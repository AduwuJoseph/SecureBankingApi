using BankingAPI.Application.Interfaces;
using BankingAPI.Domain.Entities;
using BankingAPI.Domain.Enum;
using BankingAPI.Domain.Exceptions;
using BankingAPI.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Application.Validators
{
    public class TransactionValidator : ITransactionValidator
    {
        private readonly BankingDbContext _dbContext;
        private readonly ILogger<TransactionValidator> _logger;

        public TransactionValidator(
            BankingDbContext dbContext,
            ILogger<TransactionValidator> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task ValidateTransferAsync(
            Account senderAccount,
            string recipientAccountNumber,
            decimal amount)
        {
            // Check if sender account is active
            if (!senderAccount.IsActive)
                throw new InvalidOperationException("Sender account is inactive");

            // Check for self-transfer
            if (senderAccount.AccountNumber == recipientAccountNumber)
                throw new InvalidOperationException("Cannot transfer to the same account");

            // Check minimum amount
            if (amount < 0.01m)
                throw new InvalidOperationException("Minimum transfer amount is 0.01");

            // Check maximum amount
            if (amount > 10000m)
                throw new InvalidOperationException("Maximum transfer amount is 10,000");

            // Check sufficient balance (including fee)
            var fee = CalculateFee(amount);
            var totalRequired = amount + fee;

            if (senderAccount.Balance < totalRequired)
                throw new InsufficientFundsException(
                    $"Insufficient funds. Required: {totalRequired:C}, Available: {senderAccount.Balance:C}");

            // Check daily transfer limit
            var dailyTotal = await GetDailyTransferTotal(senderAccount.UserId);
            var dailyLimit = 25000m; // Configurable

            if (dailyTotal + amount > dailyLimit)
                throw new InvalidOperationException(
                    $"Daily transfer limit exceeded. Daily limit: {dailyLimit:C}, " +
                    $"Already transferred: {dailyTotal:C}");

            // Check if recipient account exists and is active
            var recipientAccount = await _dbContext
                .GetByAccountNumberAsync(recipientAccountNumber);

            if (recipientAccount == null)
                throw new NotFoundException("Recipient account not found");

            if (!recipientAccount.IsActive)
                throw new InvalidOperationException("Recipient account is inactive");

            // Validate currency
            if (senderAccount.Currency != recipientAccount.Currency)
                throw new InvalidOperationException("Currency mismatch between accounts");

            // Anti-fraud checks
            await PerformAntiFraudChecks(senderAccount, recipientAccount, amount);
        }

        private decimal CalculateFee(decimal amount)
        {
            // Example: 0.5% fee with minimum $1 and maximum $25
            var fee = amount * 0.005m;
            fee = Math.Max(fee, 1m);
            fee = Math.Min(fee, 25m);
            return fee;
        }

        private async Task<decimal> GetDailyTransferTotal(Guid userId)
        {
            // Implementation to get today's total transfers
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            var transactions = await _dbContext.GetUserTransactionsAsync(
                userId, today, tomorrow);

            return transactions.Where(t => t.Type == TransactionType.Transfer)
                .Sum(t => t.Amount);
        }

        private async Task PerformAntiFraudChecks(
            Account sender,
            Account recipient,
            decimal amount)
        {
            // Check for unusual patterns
            var avgTransaction = await GetAverageTransactionAmount(sender.UserId);

            if (amount > avgTransaction * 5)
                throw new InvalidOperationException(
                    "Transaction flagged for unusual pattern. Please contact support.");

            // Check for rapid consecutive transfers
            var recentTransactions = await GetRecentTransactions(sender.UserId, 5);
            if (recentTransactions.Count() >= 5 &&
                recentTransactions.All(t => t.Amount > 1000))
            {
                throw new InvalidOperationException(
                    "Multiple large transfers detected. Please verify your identity.");
            }
        }
    }
}
