using BankingAPI.Application.DTOs;
using BankingAPI.Application.DTOs.Transaction;
using BankingAPI.Application.DTOs.Transfer;
using BankingAPI.Application.Interfaces;
using BankingAPI.Domain.Entities;
using BankingAPI.Domain.Enum;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Application.Services
{
    public class TransferService: ITransferService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMemoryCache _cache;
        private readonly ILogger<TransferService> _logger;
        private readonly ITransactionValidator _validator;

        public TransferService(
            IUnitOfWork unitOfWork,
            IMemoryCache cache,
            ILogger<TransferService> logger,
            ITransactionValidator validator)
        {
            _unitOfWork = unitOfWork;
            _cache = cache;
            _logger = logger;
            _validator = validator;
        }

        public async Task<ApiResponse<TransactionResponse>> TransferAsync(Guid userId, TransferRequest transferDto)
        {
            // Check idempotency
            var existingTransaction = await _unitOfWork.Transactions
                .GetByIdempotentKeyAsync(transferDto.IdempotentKey);

            if (existingTransaction != null)
            {
                _logger.LogInformation("Duplicate transfer attempt detected for key: {IdempotentKey}",
                    transferDto.IdempotentKey);
                return MapToTransactionDto(existingTransaction);
            }

            await _unitOfWork.BeginTransactionAsync();

            try
            {
                // Get sender account
                var senderAccount = await _unitOfWork.Accounts.GetAccountWithUserAsync(userId);
                if (senderAccount == null || !senderAccount.IsActive)
                    throw new InvalidOperationException("Sender account not found or inactive");

                // Validate transaction
                await _validator.ValidateTransferAsync(senderAccount,
                    transferDto.RecipientAccountNumber, transferDto.Amount);

                // Get recipient account
                var recipientAccount = await _unitOfWork.Accounts
                    .GetByAccountNumberAsync(transferDto.RecipientAccountNumber);

                if (recipientAccount == null || !recipientAccount.IsActive)
                    throw new InvalidOperationException("Recipient account not found");

                // Calculate fee (example: 0.5% with minimum $1)
                var fee = Math.Max(transferDto.Amount * 0.005m, 1m);
                var totalDebit = transferDto.Amount + fee;

                // Create transaction
                var transaction = new Transaction
                {
                    Id = Guid.NewGuid(),
                    TransactionReference = GenerateTransactionReference(),
                    IdempotentKey = transferDto.IdempotentKey,
                    SenderAccountId = senderAccount.Id,
                    RecipientAccountId = recipientAccount.Id,
                    Amount = transferDto.Amount,
                    Currency = senderAccount.Currency,
                    Description = transferDto.Description,
                    Status = TransactionStatus.Processing,
                    Type = TransactionType.Transfer,
                    Fee = fee,
                    InitiatedAt = DateTime.UtcNow
                };

                await _unitOfWork.Transactions.AddAsync(transaction);

                // Update balances with concurrency handling
                var senderNewBalance = senderAccount.Balance - totalDebit;
                var recipientNewBalance = recipientAccount.Balance + transferDto.Amount;

                // Update sender balance
                var senderUpdated = await _unitOfWork.Accounts.UpdateBalanceAsync(
                    senderAccount.Id, senderNewBalance, senderAccount.RowVersion);

                if (!senderUpdated)
                    throw new ConcurrencyException("Balance update failed. Please try again.");

                // Update recipient balance
                var recipientUpdated = await _unitOfWork.Accounts.UpdateBalanceAsync(
                    recipientAccount.Id, recipientNewBalance, recipientAccount.RowVersion);

                if (!recipientUpdated)
                    throw new ConcurrencyException("Balance update failed. Please try again.");

                // Create ledger entries
                await CreateLedgerEntries(senderAccount.Id, transaction.Id,
                    senderAccount.Balance, senderNewBalance, totalDebit, LedgerEntryType.Debit);

                await CreateLedgerEntries(recipientAccount.Id, transaction.Id,
                    recipientAccount.Balance, recipientNewBalance, transferDto.Amount, LedgerEntryType.Credit);

                // Update transaction status
                transaction.Status = TransactionStatus.Completed;
                transaction.CompletedAt = DateTime.UtcNow;
                await _unitOfWork.Transactions.UpdateAsync(transaction);

                await _unitOfWork.CommitTransactionAsync();

                // Clear cache for affected accounts
                _cache.Remove($"account_balance_{senderAccount.Id}");
                _cache.Remove($"account_balance_{recipientAccount.Id}");

                _logger.LogInformation("Transfer completed successfully. Reference: {Reference}",
                    transaction.TransactionReference);

                return MapToTransactionDto(transaction);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                _logger.LogError(ex, "Transfer failed for user {UserId}", userId);
                throw;
            }
        }

        public async Task<TransactionDto> GetTransactionStatusAsync(string transactionReference)
        {
            var transaction = await _unitOfWork.Transactions
                .GetByReferenceAsync(transactionReference);

            if (transaction == null)
                throw new NotFoundException("Transaction not found");

            return MapToTransactionDto(transaction);
        }

        private async Task CreateLedgerEntries(Guid accountId, Guid transactionId,
            decimal previousBalance, decimal newBalance, decimal amount, LedgerEntryType entryType)
        {
            var ledger = new AccountLedger
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                TransactionId = transactionId,
                PreviousBalance = previousBalance,
                NewBalance = newBalance,
                Amount = amount,
                EntryType = entryType,
                Description = entryType == LedgerEntryType.Debit ? "Debit transaction" : "Credit transaction",
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.AccountLedgers.AddAsync(ledger);
        }

        private string GenerateTransactionReference()
        {
            return $"TXN{DateTime.UtcNow:yyyyMMddHHmmss}{Guid.NewGuid():N}".Substring(0, 20);
        }

        private TransactionResponse MapToTransactionDto(Transaction transaction)
        {
            return new TransactionResponse
            {
                Id = transaction.Id,
                TransactionReference = transaction.TransactionReference,
                Amount = transaction.Amount,
                Currency = transaction.Currency,
                Description = transaction.Description,
                Status = transaction.Status.ToString(),
                InitiatedAt = transaction.InitiatedAt,
                CompletedAt = transaction.CompletedAt
            };
        }
        public async Task<TransactionResponse> TransferAsync(TransferRequest request, string idempotencyKey)
        {
            if (await _idempotencyService.ExistsAsync(idempotencyKey))
                return await _idempotencyService.GetResponseAsync(idempotencyKey);

            using var transaction = await _dbContext.Database.BeginTransactionAsync();

            var sender = await _accountRepo.LockByIdAsync(request.SenderAccountId);
            var receiver = await _accountRepo.LockByAccountNumberAsync(request.ReceiverAccountNumber);

            if (sender.Balance < request.Amount)
                throw new Exception("Insufficient funds");

            sender.Balance -= request.Amount;
            receiver.Balance += request.Amount;

            var tx = new Transaction
            {
                SenderAccountId = sender.Id,
                ReceiverAccountId = receiver.Id,
                Amount = request.Amount,
                Status = TransactionStatus.Success
            };

            await _transactionRepo.AddAsync(tx);
            await _unitOfWork.SaveChangesAsync();

            await _idempotencyService.SaveAsync(idempotencyKey, tx);

            await transaction.CommitAsync();

            return _mapper.Map<TransactionResponse>(tx);
        }


    }
}
