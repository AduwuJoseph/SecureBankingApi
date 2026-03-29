using BankingAPI.Application.DTOs.Transfer;
using BankingAPI.Application.Interfaces;
using BankingAPI.Application.Services;
using BankingAPI.Domain.Entities;
using BankingAPI.Infrastructure.Services.Validators;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace BankingAPI.UnitTests.Services;

public class TransactionValidatorTests
{
    private readonly IBankingDbContext _dbContext;
    private readonly ILogger<TransactionValidator> _logger;
    private readonly IConfiguration _configuration;
    private readonly TransactionValidator _validator;

    public TransactionValidatorTests()
    {
        _dbContext = Substitute.For<IBankingDbContext>();
        _logger = Substitute.For<ILogger<TransactionValidator>>();
        _configuration = Substitute.For<IConfiguration>();

        SetupConfiguration();
        _validator = new TransactionValidator(_dbContext, _logger, _configuration);
    }

    private void SetupConfiguration()
    {
        var configValues = new Dictionary<string, string>
        {
            ["TransactionLimits:MinimumAmount"] = "0.01",
            ["TransactionLimits:MaximumAmount"] = "10000",
            ["TransactionLimits:DailyLimit"] = "25000",
            ["TransactionFees:Percentage"] = "0.005",
            ["TransactionFees:Minimum"] = "1",
            ["TransactionFees:Maximum"] = "25",
            ["AntiFraud:UnusualPatternMultiplier"] = "5"
        };

        _configuration.GetValue<decimal>(Arg.Any<string>(), Arg.Any<decimal>())
            .Returns(callInfo =>
            {
                var key = callInfo.Arg<string>();
                return configValues.ContainsKey(key)
                    ? decimal.Parse(configValues[key])
                    : callInfo.Arg<decimal>();
            });
    }

    [Fact]
    public async Task ValidateTransferAsync_ValidTransfer_ShouldSucceed()
    {
        // Arrange
        var senderAccount = new Account
        {
            AccountNumber = "123456",
            UserId = 1,
            Balance = 1000,
            IsActive = true,
            Currency = "USD"
        };

        var recipientAccount = new Account
        {
            AccountNumber = "789012",
            UserId = 2,
            IsActive = true,
            Currency = "USD"
        };

        _dbContext.Accounts.FirstOrDefaultAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Account, bool>>>())
            .Returns(recipientAccount);

        _dbContext.Transactions.Where(Arg.Any<System.Linq.Expressions.Expression<Func<Transaction, bool>>>())
            .Returns(Substitute.For<IQueryable<Transaction>>());

        //_antiFraudService.CheckTransactionAsync(Arg.Any<Account>(), Arg.Any<Account>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>())
        //    .Returns(new FraudCheckResult { IsApproved = true });

        // Act
        var result = await _validator.ValidateTransferAsync(senderAccount, "789012", 100);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Fee.Should().Be(1m); // Minimum fee
        result.TotalAmount.Should().Be(101m);
    }

    [Fact]
    public async Task ValidateTransferAsync_InsufficientBalance_ShouldFail()
    {
        // Arrange
        var senderAccount = new Account
        {
            AccountNumber = "123456",
            UserId = 1,
            Balance = 50,
            IsActive = true,
            Currency = "USD"
        };

        // Act
        var result = await _validator.ValidateTransferAsync(senderAccount, "789012", 100);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("Insufficient funds"));
        result.Errors.Should().Contain(e => e.Code == "InsufficientFunds");
    }

    [Fact]
    public async Task ValidateTransferAsync_SelfTransfer_ShouldFail()
    {
        // Arrange
        var senderAccount = new Account
        {
            AccountNumber = "123456",
            UserId = 1,
            Balance = 1000,
            IsActive = true,
            Currency = "USD"
        };

        // Act
        var result = await _validator.ValidateTransferAsync(senderAccount, "123456", 100);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("Cannot transfer to the same account"));
    }

    [Fact]
    public async Task ValidateTransferAsync_InactiveAccount_ShouldFail()
    {
        // Arrange
        var senderAccount = new Account
        {
            AccountNumber = "123456",
            UserId = 1,
            Balance = 1000,
            IsActive = false,
            Currency = "USD"
        };

        // Act
        var result = await _validator.ValidateTransferAsync(senderAccount, "789012", 100);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("inactive"));
    }

    [Fact]
    public async Task ValidateTransferAsync_AmountBelowMinimum_ShouldFail()
    {
        // Arrange
        var senderAccount = new Account
        {
            AccountNumber = "123456",
            UserId = 1,
            Balance = 1000,
            IsActive = true,
            Currency = "USD"
        };

        // Act
        var result = await _validator.ValidateTransferAsync(senderAccount, "789012", 0.001m);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("Minimum transfer amount"));
    }

    [Fact]
    public async Task ValidateTransferAsync_CurrencyMismatch_ShouldFail()
    {
        // Arrange
        var senderAccount = new Account
        {
            AccountNumber = "123456",
            UserId = 1,
            Balance = 1000,
            IsActive = true,
            Currency = "USD"
        };

        var recipientAccount = new Account
        {
            AccountNumber = "789012",
            UserId = 2,
            IsActive = true,
            Currency = "EUR"
        };

        _dbContext.Accounts.FirstOrDefaultAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Account, bool>>>())
            .Returns(recipientAccount);

        // Act
        var result = await _validator.ValidateTransferAsync(senderAccount, "789012", 100);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("Currency mismatch"));
    }
}