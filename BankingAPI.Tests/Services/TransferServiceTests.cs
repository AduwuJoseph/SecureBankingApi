using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using FluentAssertions;
using BankingAPI.Application.DTOs.Transfer;
using BankingAPI.Application.Interfaces;
using BankingAPI.Application.Services;
using BankingAPI.Domain.Entities;
using BankingAPI.Domain.Enum;
using BankingAPI.Domain.Exceptions;
using BankingAPI.Infrastructure.Data;
using BankingAPI.Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;

namespace BankingAPI.UnitTests.Services;

public class TransferServiceTests
{
    private readonly IBankingDbContext _context;
    private readonly ILogger<TransferService> _logger;
    private readonly IAuditService _auditService;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    private readonly ITransactionValidator _transactionValidator;
    private readonly TransferService _transferService;

    public TransferServiceTests()
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
            new Account { UserId = 1, Balance = 1000 },
            new Account { UserId = 2, Balance = 500 }
        );

        _context.SaveChangesAsync();
    }

    [Fact]
    public async Task TransferFundsAsync_ValidTransfer_ShouldSucceed()
    {
        // Arrange
        var transferDto = new TransferRequest
        {
            RecipientAccountNumber = "user2@test.com",
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
        result.Message.Should().Be("Transfer successful");
        result.TransactionReference.Should().NotBeNull();
        result.NewBalance.Should().Be(900);

        // Verify balances
        var senderAccount = await _context.Accounts.FindAsync(1);
        var recipientAccount = await _context.Accounts.FindAsync(2);
        senderAccount!.Balance.Should().Be(900);
        recipientAccount!.Balance.Should().Be(600);

        // Verify transaction was recorded
        var transaction = await _context.Transactions.FirstOrDefaultAsync();
        transaction.Should().NotBeNull();
        transaction!.Amount.Should().Be(100);
        transaction.Status.Should().Be(TransactionStatus.Completed);

        // Verify ledger entries
        var ledgerEntries = await _context.AccountLedgers.ToListAsync();
        ledgerEntries.Should().HaveCount(2);

        // Verify audit log was called
        await _auditService.Received(1).LogAsync(
            Arg.Is<string>(s => s == "Transfer"),
            Arg.Is<string>(s => s.Contains("100")),
            Arg.Is<string>(s => s == "1"));
    }

    [Fact]
    public async Task TransferFundsAsync_InsufficientBalance_ShouldThrowBusinessRuleException()
    {
        // Arrange
        var transferDto = new TransferRequest
        {
            RecipientAccountNumber = "9304949499",
            Amount = 2000,
            Description = "Too much"
        };
        var idempotencyKey = Guid.NewGuid().ToString();

        _idempotencyService.GetCachedResponseAsync(idempotencyKey)
            .Returns(Task.FromResult<TransferResponse?>(null));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _transferService.TransferFundsAsync(1, transferDto, idempotencyKey));

        exception.Message.Should().Contain("Insufficient balance");
    }

    [Fact]
    public async Task TransferFundsAsync_RecipientNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var transferDto = new TransferRequest
        {
            RecipientAccountNumber = "7484939485",
            Amount = 100,
            Description = "Test"
        };
        var idempotencyKey = Guid.NewGuid().ToString();

        _idempotencyService.GetCachedResponseAsync(idempotencyKey)
            .Returns(Task.FromResult<TransferResponse?>(null));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            _transferService.TransferFundsAsync(1, transferDto, idempotencyKey));

        exception.Message.Should().Contain("Recipient with email");
    }

    [Fact]
    public async Task TransferFundsAsync_SelfTransfer_ShouldThrowBusinessRuleException()
    {
        // Arrange
        var transferDto = new TransferRequest
        {
            RecipientAccountNumber = "7484939485",
            Amount = 100,
            Description = "Self transfer"
        };
        var idempotencyKey = Guid.NewGuid().ToString();

        _idempotencyService.GetCachedResponseAsync(idempotencyKey)
            .Returns(Task.FromResult<TransferResponse?>(null));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _transferService.TransferFundsAsync(1, transferDto, idempotencyKey));

        exception.Message.Should().Be("Cannot transfer money to yourself");
    }

    [Fact]
    public async Task TransferFundsAsync_DuplicateRequest_ShouldReturnCachedResponse()
    {
        // Arrange
        var transferDto = new TransferRequest
        {
            RecipientAccountNumber = "7484939485",
            Amount = 100,
            Description = "Test transfer"
        };
        var idempotencyKey = Guid.NewGuid().ToString();

        var cachedResponse = new TransferResponse
        {
            Success = true,
            Message = "Transfer successful (cached)",
            TransactionReference = "999",
            NewBalance = 900,
            IsIdempotentResponse = true
        };

        _idempotencyService.GetCachedResponseAsync(idempotencyKey)
            .Returns(Task.FromResult<TransferResponse?>(cachedResponse));

        // Act
        var result = await _transferService.TransferFundsAsync(1, transferDto, idempotencyKey);

        // Assert
        result.Success.Should().BeTrue();
        result.IsIdempotentResponse.Should().BeTrue();
        result.Message.Should().Be("Transfer successful (cached response)");

        // Verify no new transaction was created
        var transactions = await _context.Transactions.ToListAsync();
        transactions.Should().HaveCount(0);
    }

    [Fact]
    public async Task TransferFundsAsync_ShouldCreateLedgerEntries()
    {
        // Arrange
        var transferDto = new TransferRequest
        {
            RecipientAccountNumber = "7484939485",
            Amount = 150,
            Description = "Test with ledger"
        };
        var idempotencyKey = Guid.NewGuid().ToString();

        _idempotencyService.GetCachedResponseAsync(idempotencyKey)
            .Returns(Task.FromResult<TransferResponse?>(null));

        // Act
        await _transferService.TransferFundsAsync(1, transferDto, idempotencyKey);

        // Assert
        var ledgerEntries = await _context.AccountLedgers
            .OrderBy(l => l.UserId)
            .ToListAsync();

        ledgerEntries.Should().HaveCount(2);

        var senderLedger = ledgerEntries.First(l => l.UserId == 1);
        senderLedger.EntryType.Should().Be(LedgerEntryType.Debit);
        senderLedger.Amount.Should().Be(150);
        senderLedger.PreviousBalance.Should().Be(1000);
        senderLedger.NewBalance.Should().Be(850);

        var recipientLedger = ledgerEntries.First(l => l.UserId == 2);
        recipientLedger.EntryType.Should().Be(LedgerEntryType.Credit);
        recipientLedger.Amount.Should().Be(150);
        recipientLedger.PreviousBalance.Should().Be(500);
        recipientLedger.NewBalance.Should().Be(650);
    }
}