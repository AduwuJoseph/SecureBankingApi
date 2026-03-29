using BankingAPI.Application.DTOs.Transfer;
using BankingAPI.Application.Interfaces;
using BankingAPI.Domain.Entities;
using BankingAPI.Domain.Enum;
using BankingAPI.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BankingAPI.Infrastructure.Services.Validators;

public class TransactionValidator : ITransactionValidator
{
    private readonly IBankingDbContext _dbContext;
    private readonly ILogger<TransactionValidator> _logger;
    private readonly IConfiguration _configuration;

    // Configurable limits
    private readonly decimal _minimumTransferAmount;
    private readonly decimal _maximumTransferAmount;
    private readonly decimal _dailyTransferLimit;
    private readonly decimal _feePercentage;
    private readonly decimal _minimumFee;
    private readonly decimal _maximumFee;
    private readonly decimal _unusualPatternMultiplier;

    public TransactionValidator(
        IBankingDbContext dbContext,
        ILogger<TransactionValidator> logger,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _logger = logger;
        _configuration = configuration;

        // Load configuration values
        _minimumTransferAmount = configuration.GetValue<decimal>("TransactionLimits:MinimumAmount", 0.01m);
        _maximumTransferAmount = configuration.GetValue<decimal>("TransactionLimits:MaximumAmount", 10000m);
        _dailyTransferLimit = configuration.GetValue<decimal>("TransactionLimits:DailyLimit", 25000m);
        _feePercentage = configuration.GetValue<decimal>("TransactionFees:Percentage", 0.005m);
        _minimumFee = configuration.GetValue<decimal>("TransactionFees:Minimum", 1m);
        _maximumFee = configuration.GetValue<decimal>("TransactionFees:Maximum", 25m);
        _unusualPatternMultiplier = configuration.GetValue<decimal>("AntiFraud:UnusualPatternMultiplier", 5m);
    }

    public async Task<ValidationResult> ValidateTransferAsync(
        Account senderAccount,
        string recipientAccountNumber,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        var validationResult = new ValidationResult();

        try
        {
            // Validate sender account
            ValidateSenderAccount(senderAccount, validationResult);

            // Validate self-transfer
            ValidateSelfTransfer(senderAccount.AccountNumber, recipientAccountNumber, validationResult);

            // Validate amount limits
            ValidateAmountLimits(amount, validationResult);

            if (!validationResult.IsValid)
                return validationResult;

            // Calculate fee and validate balance
            var fee = CalculateFee(amount);
            var totalRequired = amount + fee;

            ValidateBalance(senderAccount.Balance, totalRequired, validationResult);

            // Validate daily transfer limit
            await ValidateDailyTransferLimitAsync(senderAccount.UserId, amount, validationResult, cancellationToken);

            if (!validationResult.IsValid)
                return validationResult;

            // Validate recipient account
            var recipientAccount = await ValidateRecipientAccountAsync(
                recipientAccountNumber, validationResult, cancellationToken);

            if (!validationResult.IsValid)
                return validationResult;

            // Validate currency
            ValidateCurrency(senderAccount.Currency, recipientAccount!.Currency, validationResult);

            // Perform anti-fraud checks
            await PerformAntiFraudChecksAsync(
                senderAccount, recipientAccount!, amount, validationResult, cancellationToken);

            // Set fee if validation passed
            if (validationResult.IsValid)
            {
                validationResult.Fee = fee;
                validationResult.TotalAmount = totalRequired;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during transaction validation");
            validationResult.AddError("An unexpected error occurred during validation");
        }

        return validationResult;
    }

    #region Private Validation Methods

    private void ValidateSenderAccount(Account senderAccount, ValidationResult result)
    {
        if (senderAccount == null)
        {
            result.AddError("Sender account not found");
            return;
        }

        if (!senderAccount.IsActive)
        {
            result.AddError("Sender account is inactive. Please contact support.");
            _logger.LogWarning("Attempted transfer from inactive account {AccountNumber}",
                senderAccount.AccountNumber);
        }
    }

    private void ValidateSelfTransfer(string senderAccountNumber, string recipientAccountNumber, ValidationResult result)
    {
        if (senderAccountNumber == recipientAccountNumber)
        {
            result.AddError("Cannot transfer to the same account");
        }
    }

    private void ValidateAmountLimits(decimal amount, ValidationResult result)
    {
        if (amount < _minimumTransferAmount)
        {
            result.AddError($"Minimum transfer amount is {_minimumTransferAmount:C}");
        }

        if (amount > _maximumTransferAmount)
        {
            result.AddError($"Maximum transfer amount is {_maximumTransferAmount:C}");
        }
    }

    private void ValidateBalance(decimal currentBalance, decimal requiredAmount, ValidationResult result)
    {
        if (currentBalance < requiredAmount)
        {
            result.AddError(
                $"Insufficient funds. Required: {requiredAmount:C}, Available: {currentBalance:C}",
                "InsufficientFunds");
        }
    }

    private async Task ValidateDailyTransferLimitAsync(
        int userId,
        decimal amount,
        ValidationResult result,
        CancellationToken cancellationToken)
    {
        var dailyTotal = await GetDailyTransferTotalAsync(userId, cancellationToken);

        if (dailyTotal + amount > _dailyTransferLimit)
        {
            result.AddError(
                $"Daily transfer limit exceeded. Daily limit: {_dailyTransferLimit:C}, " +
                $"Already transferred: {dailyTotal:C}, Attempting: {amount:C}",
                "DailyLimitExceeded");

            _logger.LogWarning(
                "Daily limit exceeded for user {UserId}. Total: {Total}, Attempt: {Amount}",
                userId, dailyTotal, amount);
        }
    }

    private async Task<Account?> ValidateRecipientAccountAsync(
        string accountNumber,
        ValidationResult result,
        CancellationToken cancellationToken)
    {
        var recipientAccount = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.AccountNumber == accountNumber, cancellationToken);

        if (recipientAccount == null)
        {
            result.AddError("Recipient account not found");
            return null;
        }

        if (!recipientAccount.IsActive)
        {
            result.AddError("Recipient account is inactive");
            return null;
        }

        return recipientAccount;
    }

    private void ValidateCurrency(string senderCurrency, string recipientCurrency, ValidationResult result)
    {
        if (senderCurrency != recipientCurrency)
        {
            result.AddError(
                $"Currency mismatch. Sender currency: {senderCurrency}, " +
                $"Recipient currency: {recipientCurrency}");
        }
    }

    private async Task PerformAntiFraudChecksAsync(
        Account senderAccount,
        Account recipientAccount,
        decimal amount,
        ValidationResult result,
        CancellationToken cancellationToken)
    {
        // Check for unusual transaction patterns
        var avgTransaction = await GetAverageTransactionAmountAsync(senderAccount.UserId, cancellationToken);

        if (avgTransaction > 0 && amount > avgTransaction * _unusualPatternMultiplier)
        {
            var warning = $"Transaction amount {amount:C} exceeds unusual pattern threshold " +
                         $"(Average: {avgTransaction:C})";

            result.AddWarning(warning);

            _logger.LogInformation(
                "Unusual transaction pattern detected for user {UserId}. Amount: {Amount}, Average: {Avg}",
                senderAccount.UserId, amount, avgTransaction);
        }

        // Check for rapid consecutive large transfers
        var recentLargeTransfers = await GetRecentLargeTransfersAsync(
            senderAccount.UserId,
            amount,
            cancellationToken);

        if (recentLargeTransfers >= 5)
        {
            result.AddError(
                "Multiple large transfers detected. Please verify your identity or contact support.",
                "SuspiciousActivity");

            _logger.LogWarning(
                "Suspicious activity detected for user {UserId}: {Count} large transfers in last hour",
                senderAccount.UserId, recentLargeTransfers);
        }

        // Call external anti-fraud service if configured
        //var fraudCheckResult = await _antiFraudService.CheckTransactionAsync(
        //    senderAccount, recipientAccount, amount, cancellationToken);

        //if (!fraudCheckResult.IsApproved)
        //{
        //    result.AddError(fraudCheckResult.Reason ?? "Transaction flagged by fraud detection system");
        //}
    }

    #endregion

    #region Helper Methods

    private decimal CalculateFee(decimal amount)
    {
        var fee = amount * _feePercentage;
        fee = Math.Max(fee, _minimumFee);
        fee = Math.Min(fee, _maximumFee);
        return fee;
    }

    private async Task<decimal> GetDailyTransferTotalAsync(int userId, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var transactions = await _dbContext.Transactions
            .Where(t => t.SenderId == userId &&
                       t.Timestamp >= today &&
                       t.Timestamp < tomorrow &&
                       t.Status == TransactionStatus.Completed)
            .SumAsync(t => t.Amount, cancellationToken);

        return transactions;
    }

    private async Task<decimal> GetAverageTransactionAmountAsync(int userId, CancellationToken cancellationToken)
    {
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

        var average = await _dbContext.Transactions
            .Where(t => t.SenderId == userId &&
                       t.Timestamp >= thirtyDaysAgo &&
                       t.Status == TransactionStatus.Completed)
            .AverageAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0;

        return average;
    }

    private async Task<int> GetRecentLargeTransfersAsync(int userId, decimal currentAmount, CancellationToken cancellationToken)
    {
        var oneHourAgo = DateTime.UtcNow.AddHours(-1);
        var threshold = Math.Max(1000m, currentAmount * 0.5m);

        var count = await _dbContext.Transactions
            .Where(t => t.SenderId == userId &&
                       t.Timestamp >= oneHourAgo &&
                       t.Amount >= threshold &&
                       t.Status == TransactionStatus.Completed)
            .CountAsync(cancellationToken);

        return count;
    }

    #endregion
}