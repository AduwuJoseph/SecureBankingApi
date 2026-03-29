using BankingAPI.Application.DTOs.Transfer;
using BankingAPI.Application.Interfaces;
using BankingAPI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace BankingAPI.Infrastructure.Services;

public class MockAntiFraudService : IAntiFraudService
{
    private readonly ILogger<MockAntiFraudService> _logger;

    public MockAntiFraudService(ILogger<MockAntiFraudService> logger)
    {
        _logger = logger;
    }

    public Task<FraudCheckResult> CheckTransactionAsync(
        Account senderAccount,
        Account recipientAccount,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        // Mock implementation - always approves
        _logger.LogDebug("Anti-fraud check passed for transaction from {Sender} to {Recipient}",
            senderAccount.AccountNumber, recipientAccount.AccountNumber);

        return Task.FromResult(new FraudCheckResult
        {
            IsApproved = true,
            RiskScore = 0.1m
        });
    }
}