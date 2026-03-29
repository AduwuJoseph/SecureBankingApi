using BankingAPI.Application.DTOs.Errors;
using BankingAPI.Application.DTOs.Transfer;
using BankingAPI.Application.Interfaces;
using BankingAPI.Domain.Entities;
using BankingAPI.Domain.Enum;
using BankingAPI.Domain.Exceptions;
using BankingAPI.Infrastructure.Data;
using BankingAPI.Infrastructure.Services.Validators;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace BankingAPI.Infrastructure.Services;

public class TransferService : ITransferService
{
    private readonly IBankingDbContext _context;
    private readonly ILogger<TransferService> _logger;
    private readonly IAuditService _auditService;
    private readonly IIdempotencyService _idempotencyService;
    private readonly ITransactionValidator _transactionValidator;
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    private readonly JsonSerializerOptions _jsonOptions;
    private const int MaxRetryAttempts = 3;

    public TransferService(
        IBankingDbContext context,
        ILogger<TransferService> logger,
        IAuditService auditService,
        IIdempotencyService idempotencyService,
        IMemoryCache memoryCache,
        IDistributedCache distributedCache,
        ITransactionValidator transactionValidator)
    {
        _context = context;
        _logger = logger;
        _auditService = auditService;
        _idempotencyService = idempotencyService;
        _memoryCache = memoryCache;
        _distributedCache = distributedCache;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        _transactionValidator = transactionValidator;
    }

    public async Task<TransferResponse> TransferFundsAsync(
        int senderId,
        TransferRequest transferDto,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        // Check if cancellation is requested
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            "Starting transfer process. SenderId: {SenderId}, Recipient: {RecipientEmail}, Amount: {Amount}, IdempotencyKey: {IdempotencyKey}",
            senderId, transferDto.RecipientAccountNumber, transferDto.Amount, idempotencyKey);

        try
        {
            // Check idempotency first
            var cachedResponse = await _idempotencyService.GetCachedResponseAsync(idempotencyKey);
            if (cachedResponse != null)
            {
                _logger.LogInformation(
                    "Returning cached response for idempotency key {IdempotencyKey}",
                    idempotencyKey);

                return new TransferResponse
                {
                    Success = true,
                    Message = "Transfer successful (cached response)",
                    TransactionReference = cachedResponse.TransactionReference,
                    NewBalance = cachedResponse.NewBalance,
                    IsIdempotentResponse = true,
                    RowVersion = cachedResponse.RowVersion
                };
            }

            // Implement retry logic for concurrency conflicts
            int retryCount = 0;
            while (retryCount < MaxRetryAttempts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    return await ExecuteTransferWithConcurrencyAsync(
                        senderId,
                        transferDto,
                        idempotencyKey,
                        cancellationToken);
                }
                catch (DbUpdateConcurrencyException ex) when (retryCount < MaxRetryAttempts - 1)
                {
                    retryCount++;
                    _logger.LogWarning(
                        ex,
                        "Concurrency conflict during transfer attempt {RetryCount} for user {SenderId}. Retrying...",
                        retryCount,
                        senderId);

                    // Exponential backoff
                    var delay = TimeSpan.FromMilliseconds(100 * Math.Pow(2, retryCount));
                    await Task.Delay(delay, cancellationToken);

                    // Reload entities with fresh data
                    await ReloadAccountEntitiesAsync(senderId, transferDto.RecipientAccountNumber, cancellationToken);
                }
            }

            throw new ConcurrencyException(
                "Unable to complete transfer due to concurrent operations. Please try again.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Transfer operation was cancelled for user {SenderId}",
                senderId);
            throw;
        }
        catch (Exception ex) when (ex is not DomainException)
        {
            _logger.LogError(
                ex,
                "Unexpected error during transfer for user {SenderId}",
                senderId);
            throw new BusinessRuleException("An unexpected error occurred during transfer");
        }
    }

    public async Task<decimal> GetDailyTransferTotalAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            var cacheKey = $"daily_transfer_total_{userId}_{today:yyyyMMdd}";

            // Try to get from cache
            if (_memoryCache.TryGetValue(cacheKey, out decimal cachedTotal))
            {
                _logger.LogDebug("Daily transfer total retrieved from cache for user {UserId}", userId);
                return cachedTotal;
            }

            // Try distributed cache
            var cachedJson = await _distributedCache.GetStringAsync(cacheKey, cancellationToken);
            if (!string.IsNullOrEmpty(cachedJson))
            {
                var totalData = decimal.Parse(cachedJson);
                _memoryCache.Set(cacheKey, totalData, TimeSpan.FromMinutes(5));
                return totalData;
            }

            // Calculate from database
            var total = await _context.Transactions
                .Where(t => t.SenderId == userId &&
                           t.Timestamp >= today &&
                           t.Timestamp < tomorrow &&
                           t.Status == TransactionStatus.Completed)
                .SumAsync(t => t.Amount, cancellationToken);

            // Cache the result
            _memoryCache.Set(cacheKey, total, TimeSpan.FromMinutes(5));
            await _distributedCache.SetStringAsync(
                cacheKey,
                total.ToString(),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                },
                cancellationToken);

            return total;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("GetDailyTransferTotalAsync was cancelled for user {UserId}", userId);
            throw;
        }
    }

    #region Private Methods

    private async Task<TransferResponse> ExecuteTransferWithConcurrencyAsync(
        int senderId,
        TransferRequest transferRequest,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var dbTransaction = await _context.BeginTransactionAsync(cancellationToken);

        try
        {
            // Get sender's account with concurrency token tracking
            var senderAccount = await _context.Accounts
                .FirstOrDefaultAsync(a => a.UserId == senderId, cancellationToken);

            if (senderAccount == null)
            {
                throw new NotFoundException($"Account for user {senderId} not found");
            }

            // Store original row version for tracking
            var originalRowVersion = senderAccount.RowVersion;

            // Check sufficient balance
            if (senderAccount.Balance < transferRequest.Amount)
            {
                throw new BusinessRuleException(
                    $"Insufficient balance. Current balance: {senderAccount.Balance:C}");
            }

            // Get recipient user
            var recipient = await _context.Accounts
                .FirstOrDefaultAsync(u => u.AccountNumber == transferRequest.RecipientAccountNumber, cancellationToken);

            if (recipient == null)
            {
                throw new NotFoundException($"Recipient with account number {transferRequest.RecipientAccountNumber} not found");
            }

            // Prevent self-transfer
            if (senderId == recipient.Id)
            {
                throw new BusinessRuleException("Cannot transfer money to yourself");
            }

            // Get recipient's account with concurrency token tracking
            var recipientAccount = await _context.Accounts
                .FirstOrDefaultAsync(a => a.UserId == recipient.Id, cancellationToken);

            if (recipientAccount == null)
            {
                throw new NotFoundException($"Account for recipient {recipient.Id} not found");
            }


            // Validate transfer before processing
            var validationResult = await _transactionValidator.ValidateTransferAsync(
                senderAccount,
                transferRequest.RecipientAccountNumber,
                transferRequest.Amount,
                cancellationToken);

            if (!validationResult.IsValid)
            {
                _logger.LogWarning(
                    "Transfer validation failed for User {UserId}: {Errors}",
                    senderId,
                    string.Join(", ", validationResult.Errors.Select(e => e.Message)));

                throw new ValidationException($"Transfer validation failed: {validationResult.Errors.Select(e => e.Message).ToList()}. Warnings: {validationResult.Warnings.ToList()}");
            }

            // Store original recipient row version
            var originalRecipientRowVersion = recipientAccount.RowVersion;

            // Store balances before update
            var senderBalanceBefore = senderAccount.Balance;
            var recipientBalanceBefore = recipientAccount.Balance;

            // Update balances
            senderAccount.Balance -= transferRequest.Amount;
            senderAccount.LastUpdated = DateTime.UtcNow;

            recipientAccount.Balance += transferRequest.Amount;
            recipientAccount.LastUpdated = DateTime.UtcNow;

            // Create transaction record
            var transaction = new Domain.Entities.Transaction
            {
                SenderId = senderId,
                RecipientId = recipient.Id,
                Amount = transferRequest.Amount,
                Description = transferRequest.Description,
                TransactionType = transferRequest.TransactionType,                
                Timestamp = DateTime.UtcNow,
                Status = TransactionStatus.Completed,
                IdempotencyKey = idempotencyKey,
                TransactionReference = GenerateTransactionReference(await CountTransactionAsync())
            };

            await _context.Transactions.AddAsync(transaction, cancellationToken);

            // Create ledger entries
            var senderLedger = new AccountLedger
            {
                UserId = senderId,
                TransactionId  = transaction.Id,
                EntryType = LedgerEntryType.Debit,
                Amount = transferRequest.Amount,
                PreviousBalance = senderBalanceBefore,
                NewBalance = senderAccount.Balance,
                Description = $"Transfer to {recipient.AccountNumber}",
                CreatedAt = DateTime.UtcNow
            };

            var recipientLedger = new AccountLedger
            {
                UserId = recipient.Id,
                TransactionId  = transaction.Id, 
                EntryType = LedgerEntryType.Credit,
                Amount = transferRequest.Amount,
                PreviousBalance = recipientBalanceBefore,
                NewBalance = recipientAccount.Balance,
                Description = $"Transfer from {senderAccount.User?.Email ?? "Unknown"}",
                CreatedAt = DateTime.UtcNow
            };

            await _context.AccountLedgers.AddRangeAsync(
                new[] { senderLedger, recipientLedger },
                cancellationToken);

            // Save changes - this will trigger concurrency check
            await _context.SaveChangesAsync(cancellationToken);

            // Update ledger entries with correct transaction reference
            senderLedger.TransactionId  = transaction.Id;
            recipientLedger.TransactionId  = transaction.Id;

            await _context.SaveChangesAsync(cancellationToken);

            // Commit the transaction
            await dbTransaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Transfer successful: {Amount:C} from user {SenderId} to user {RecipientId}, " +
                "TransactionId: {TransactionId}, " +
                "Sender RowVersion changed from {OldVersion} to {NewVersion}, " +
                "Recipient RowVersion changed from {OldRecipientVersion} to {NewRecipientVersion}",
                transferRequest.Amount,
                senderId,
                recipient.Id,
                transaction.Id,
                Convert.ToBase64String(originalRowVersion),
                Convert.ToBase64String(senderAccount.RowVersion),
                Convert.ToBase64String(originalRecipientRowVersion),
                Convert.ToBase64String(recipientAccount.RowVersion));

            // Log audit
            await _auditService.LogAsync(
                "Transfer",
                $"Transfer of {transferRequest.Amount:C} from {senderId} to {recipient.Id}, TransactionId: {transaction.Id}",
                senderId.ToString());

            // Invalidate caches
            await InvalidateTransferCachesAsync(senderId, cancellationToken);

            var response = new TransferResponse
            {
                Success = true,
                Message = "Transfer successful",
                TransactionReference = transaction.TransactionReference,
                NewBalance = senderAccount.Balance,
                IsIdempotentResponse = false,
                RowVersion = Convert.ToBase64String(senderAccount.RowVersion)
            };

            // Cache the response
            await _idempotencyService.CacheResponseAsync(
                idempotencyKey,
                response);

            return response;
        }
        catch (DbUpdateConcurrencyException ex) when (cancellationToken.IsCancellationRequested == false)
        {
            await dbTransaction.RollbackAsync(cancellationToken);

            // Log concurrency conflict details
            await LogConcurrencyConflictAsync(ex, senderId, cancellationToken);

            throw new ConcurrencyException(
                "The account balance was modified by another transaction. Please refresh and try again.");
        }
        catch (Exception ex) when (ex is not DomainException && ex is not OperationCanceledException)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error during transfer execution for user {SenderId}", senderId);
            throw;
        }
    }

    private string GenerateTransactionReference(long? transactionId)
    {
        return $"TXN-{DateTime.UtcNow:yyyyMMddHHmmss}-{transactionId ?? 0:D6}";
    }
    private async Task ReloadAccountEntitiesAsync(
        int senderId,
        string recipientAccountNumber,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // Reload sender account
            var senderAccount = await _context.Accounts
                .FirstOrDefaultAsync(a => a.UserId == senderId, cancellationToken);

            if (senderAccount != null)
            {
                await _context.ReloadEntityAsync(senderAccount, cancellationToken);
            }

            // Find and reload recipient account

            var recipientAccount = await _context.Accounts
                .FirstOrDefaultAsync(a => a.AccountNumber == recipientAccountNumber, cancellationToken);

            if (recipientAccount != null)
            {
                await _context.ReloadEntityAsync(recipientAccount, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("ReloadAccountEntitiesAsync was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reloading account entities during retry");
        }
    }

    private async Task<long> CountTransactionAsync() 
    {         
        try
        {
            return await _context.Transactions.LongCountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error counting transactions");
            throw;
        }
    }

    private async Task LogConcurrencyConflictAsync(
        DbUpdateConcurrencyException ex,
        int senderId,
        CancellationToken cancellationToken)
    {
        try
        {
            var entries = ex.Entries;
            foreach (var entry in entries)
            {
                if (entry.Entity is Account account)
                {
                    var databaseValues = await entry.GetDatabaseValuesAsync(cancellationToken);
                    if (databaseValues != null)
                    {
                        var currentBalance = entry.CurrentValues.GetValue<decimal>("Balance");
                        var dbBalance = databaseValues.GetValue<decimal>("Balance");
                        var currentRowVersion = entry.CurrentValues.GetValue<byte[]>("RowVersion");
                        var dbRowVersion = databaseValues.GetValue<byte[]>("RowVersion");

                        _logger.LogError(
                            ex,
                            "Concurrency conflict for Account {UserId}. " +
                            "Current balance: {CurrentBalance:C}, DB balance: {DBBalance:C}, " +
                            "Current version: {CurrentVersion}, DB version: {DBVersion}",
                            account.UserId,
                            currentBalance,
                            dbBalance,
                            Convert.ToBase64String(currentRowVersion ?? Array.Empty<byte>()),
                            Convert.ToBase64String(dbRowVersion ?? Array.Empty<byte>()));
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("LogConcurrencyConflictAsync was cancelled");
        }
        catch (Exception logEx)
        {
            _logger.LogError(logEx, "Error logging concurrency conflict");
        }
    }

    private async Task InvalidateTransferCachesAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Invalidate daily transfer total cache
            var today = DateTime.UtcNow.Date;
            var cacheKey = $"daily_transfer_total_{userId}_{today:yyyyMMdd}";

            _memoryCache.Remove(cacheKey);
            await _distributedCache.RemoveAsync(cacheKey, cancellationToken);

            // Invalidate recent transactions cache
            var recentCacheKey = $"recent_transactions_{userId}";
            _memoryCache.Remove(recentCacheKey);
            await _distributedCache.RemoveAsync(recentCacheKey, cancellationToken);

            // Invalidate transaction summary cache
            var summaryCacheKey = $"transaction_summary_{userId}";
            _memoryCache.Remove(summaryCacheKey);
            await _distributedCache.RemoveAsync(summaryCacheKey, cancellationToken);

            _logger.LogDebug("Transfer caches invalidated for user {UserId}", userId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("InvalidateTransferCachesAsync was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error invalidating transfer caches for user {UserId}", userId);
        }
    }

    #endregion
}