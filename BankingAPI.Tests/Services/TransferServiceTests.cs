using BankingAPI.Application.DTOs.Transfer;
using BankingAPI.Application.Interfaces;
using BankingAPI.Domain.Entities;
using BankingAPI.Domain.Enum;
using BankingAPI.Domain.Exceptions;
using BankingAPI.Infrastructure.Data;
using BankingAPI.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

namespace BankingAPI.UnitTests.Services;

public class TransferServiceTests
{
    private IBankingDbContext CreateDbContext()
    {
        return Substitute.For<IBankingDbContext>();
    }

    [Test]
    public async Task TransferFunds_Should_succeed_for_valid_request()
    {
        // Arrange
        var context = CreateDbContext();

        var sender = new Account
        {
            Id = 1,
            UserId = 10,
            Balance = 1000,
            IsActive = true,
            AccountNumber = "ACC123",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var recipient = new Account
        {
            Id = 2,
            UserId = 20,
            Balance = 500,
            AccountNumber = "ACC999",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        context.Accounts.AddRange(sender, recipient);
        await context.SaveChangesAsync();

        var logger = Substitute.For<ILogger<TransferService>>();
        var auditService = Substitute.For<IAuditService>();
        var idempotencyService = Substitute.For<IIdempotencyService>();
        var validator = Substitute.For<ITransactionValidator>();
        var memoryCache = Substitute.For<IMemoryCache>();
        var distributedCache = Substitute.For<IDistributedCache>();

        // ✅ VALID result (no errors)
        validator.ValidateTransferAsync(
            Arg.Any<Account>(),
            Arg.Any<string>(),
            Arg.Any<decimal>(),
            Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        idempotencyService.GetCachedResponseAsync(Arg.Any<string>())
            .Returns((TransferResponse)null);

        var sut = new TransferService(
            context,
            logger,
            auditService,
            idempotencyService,
            memoryCache,
            distributedCache,
            validator);

        var request = new TransferRequest
        {
            Amount = 100,
            RecipientAccountNumber = "ACC999",
            Description = "Test transfer",
            TransactionType = TransactionType.Transfer
        };

        // Act
        var result = await sut.TransferFundsAsync(10, request, "idem-key");

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.NewBalance, Is.EqualTo(900));

        await auditService.Received(1)
            .LogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public void TransferFunds_Should_throw_when_insufficient_balance()
    {
        var context = CreateDbContext();

        context.Accounts.Add(new Account
        {
            Id = 1,
            UserId = 10,
            Balance = 50,
            IsActive = true,
            AccountNumber = "ACC123",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        context.SaveChangesAsync();

        var sut = CreateSut(context);

        var request = new TransferRequest
        {
            Amount = 100,
            RecipientAccountNumber = "ACC999"
        };

        var ex = Assert.ThrowsAsync<BusinessRuleException>(async () =>
        {
            await sut.TransferFundsAsync(10, request, "idem-key");
        });

        Assert.That(ex.Message, Does.Contain("Insufficient balance"));
    }

    [Test]
    public async Task TransferFunds_Should_return_cached_response_when_idempotent()
    {
        var context = CreateDbContext();

        var idempotencyService = Substitute.For<IIdempotencyService>();

        idempotencyService.GetCachedResponseAsync("idem-key")
            .Returns(new TransferResponse
            {
                TransactionReference = "TXN-123",
                NewBalance = 800,
                RowVersion = "abc"
            });

        var sut = CreateSut(context, idempotencyService: idempotencyService);

        var result = await sut.TransferFundsAsync(10, new TransferRequest(), "idem-key");

        Assert.That(result.IsIdempotentResponse, Is.True);
        Assert.That(result.TransactionReference, Is.EqualTo("TXN-123"));
    }

    [Test]
    public void TransferFunds_Should_throw_when_validation_fails()
    {
        var context = CreateDbContext();

        context.Accounts.Add(new Account
        {
            Id = 1,
            UserId = 10,
            Balance = 1000,
            IsActive = true,
            AccountNumber = "ACC123",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        context.SaveChangesAsync();

        var validator = Substitute.For<ITransactionValidator>();

        var validationResult = new ValidationResult();
        validationResult.AddError("Invalid transfer");

        validator.ValidateTransferAsync(
            Arg.Any<Account>(),
            Arg.Any<string>(),
            Arg.Any<decimal>(),
            Arg.Any<CancellationToken>())
            .Returns(validationResult);

        var sut = CreateSut(context, validator: validator);

        var ex = Assert.ThrowsAsync<ValidationException>(async () =>
        {
            await sut.TransferFundsAsync(10, new TransferRequest
            {
                Amount = 100,
                RecipientAccountNumber = "ACC999"
            }, "idem-key");
        });

        Assert.That(ex.Message, Does.Contain("Transfer validation failed"));
    }

    private TransferService CreateSut(
        IBankingDbContext context,
        ILogger<TransferService>? logger = null,
        IAuditService? auditService = null,
        IIdempotencyService? idempotencyService = null,
        IMemoryCache? memoryCache = null,
        IDistributedCache? distributedCache = null,
        ITransactionValidator? validator = null)
    {
        return new TransferService(
            context,
            logger ?? Substitute.For<ILogger<TransferService>>(),
            auditService ?? Substitute.For<IAuditService>(),
            idempotencyService ?? Substitute.For<IIdempotencyService>(),
            memoryCache ?? Substitute.For<IMemoryCache>(),
            distributedCache ?? Substitute.For<IDistributedCache>(),
            validator ?? Substitute.For<ITransactionValidator>());
    }
}