using BankingAPI.Application.DTOs.Transfer;
using BankingAPI.Application.Interfaces;
using BankingAPI.Application.Services;
using BankingAPI.Domain.Entities;
using BankingAPI.Domain.Exceptions;
using BankingAPI.Infrastructure.Data;
using BankingAPI.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace BankingAPI.UnitTests.Services;

public class TransferServiceTransactionTests
{
    private readonly IBankingDbContext _context;
    private readonly ILogger<TransferService> _logger;
    private readonly IAuditService _auditService;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    private readonly ITransactionValidator _transactionValidator;
    private readonly TransferService _transferService;

    public TransferServiceTransactionTests()
    {
        _context = Substitute.For<IBankingDbContext>();
        _logger = Substitute.For<ILogger<TransferService>>();
        _auditService = Substitute.For<IAuditService>();
        _idempotencyService = Substitute.For<IIdempotencyService>();
        _memoryCache = Substitute.For<IMemoryCache>();
        _distributedCache = Substitute.For<IDistributedCache>();
        _transactionValidator = Substitute.For<ITransactionValidator>();
        _transferService = new TransferService(_context, _logger, _auditService, _idempotencyService, _memoryCache, _distributedCache, _transactionValidator);
        SeedDatabase();
    }

    private void SeedDatabase()
    {
        var user1 = new User { Id = 1, FullName = "User One", Email = "user1@test.com", PasswordHash = "hash" };
        var user2 = new User { Id = 2, FullName = "User Two", Email = "user2@test.com", PasswordHash = "hash" };

        _context.Users.AddRange(user1, user2);

        _context.Accounts.AddRange(
            new Account { UserId = 1, Balance = 1000, RowVersion = new byte[] { 1, 2, 3, 4 } },
            new Account { UserId = 2, Balance = 500, RowVersion = new byte[] { 5, 6, 7, 8 } }
        );

        _context.SaveChangesAsync();
    }

    [Fact]
    public async Task TransferFundsAsync_OnError_ShouldRollbackTransaction()
    {
        // Arrange
        var transferDto = new TransferRequest
        {
            RecipientAccountNumber = "648",
            Amount = 100,
            Description = "Test transfer"
        };
        var idempotencyKey = Guid.NewGuid().ToString();

        _idempotencyService.GetCachedResponseAsync(idempotencyKey)
            .Returns(Task.FromResult<TransferResponse?>(null));

        // Get initial balances
        var initialSenderBalance = (await _context.Accounts.FindAsync(1))!.Balance;
        var initialRecipientBalance = (await _context.Accounts.FindAsync(2))!.Balance;

        // Act - Force an error by making recipient email invalid
        var invalidTransferDto = new TransferRequest
        {
            RecipientAccountNumber = "648",
            Amount = 100,
            Description = "Should rollback"
        };

        // Attempt transfer that will fail
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _transferService.TransferFundsAsync(1, invalidTransferDto, idempotencyKey));

        // Assert - Verify no changes were made
        var finalSenderBalance = (await _context.Accounts.FindAsync(1))!.Balance;
        var finalRecipientBalance = (await _context.Accounts.FindAsync(2))!.Balance;
        var transactionCount = await _context.Transactions.CountAsync();
        var ledgerCount = await _context.AccountLedgers.CountAsync();

        finalSenderBalance.Should().Be(initialSenderBalance);
        finalRecipientBalance.Should().Be(initialRecipientBalance);
        transactionCount.Should().Be(0);
        ledgerCount.Should().Be(0);
    }

    [Fact]
    public async Task TransferFundsAsync_WhenSuccessful_ShouldCommitTransaction()
    {
        // Arrange
        var transferDto = new TransferRequest
        {
            RecipientAccountNumber = "7484939485",
            Amount = 100,
            Description = "Test transfer"
        };
        var idempotencyKey = Guid.NewGuid().ToString();

        _idempotencyService.GetCachedResponseAsync(idempotencyKey)
            .Returns(Task.FromResult<TransferResponse?>(null));

        // Act
        var result = await _transferService.TransferFundsAsync(1, transferDto, idempotencyKey);

        // Assert
        result.Success.Should().BeTrue();

        // Verify changes were persisted
        var senderAccount = await _context.Accounts.FindAsync(1);
        var recipientAccount = await _context.Accounts.FindAsync(2);
        var transaction = await _context.Transactions.FirstOrDefaultAsync();
        var ledgers = await _context.AccountLedgers.ToListAsync();

        senderAccount!.Balance.Should().Be(900);
        recipientAccount!.Balance.Should().Be(600);
        transaction.Should().NotBeNull();
        ledgers.Should().HaveCount(2);
    }

    [Fact]
    public async Task TransferFundsAsync_WhenDbUpdateConcurrencyException_ShouldRollback()
    {
        // Arrange
        var transferDto = new TransferRequest
        {
            RecipientAccountNumber = "74349394930",
            Amount = 100,
            Description = "Concurrent test"
        };
        var idempotencyKey = Guid.NewGuid().ToString();

        _idempotencyService.GetCachedResponseAsync(idempotencyKey)
            .Returns(Task.FromResult<TransferResponse?>(null));

        // Get initial balance
        var initialBalance = (await _context.Accounts.FindAsync(1))!.Balance;

        // Simulate concurrent modification by updating the account directly
        var account = await _context.Accounts.FindAsync(1);
        account!.Balance = 950; // Different balance to cause concurrency conflict
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConcurrencyException>(() =>
            _transferService.TransferFundsAsync(1, transferDto, idempotencyKey));

        exception.Message.Should().Contain("modified by another transaction");

        // Verify no transaction was created
        var transactionCount = await _context.Transactions.CountAsync();
        var ledgerCount = await _context.AccountLedgers.CountAsync();

        transactionCount.Should().Be(0);
        ledgerCount.Should().Be(0);

        // Balance should remain as the concurrent update set
        var finalBalance = (await _context.Accounts.FindAsync(1))!.Balance;
        finalBalance.Should().Be(950);
    }
}