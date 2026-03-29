using BankingAPI.API.Controllers;
using BankingAPI.Application.DTOs.Account;
using BankingAPI.Application.DTOs.Errors;
using BankingAPI.Application.DTOs.Transaction;
using BankingAPI.Application.DTOs.Transfer;
using BankingAPI.Application.Interfaces;
using BankingAPI.Application.Services;
using BankingAPI.Domain.Entities;
using BankingAPI.Domain.Exceptions;
using BankingAPI.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace BankingAPI.UnitTests.Integration;

public class TransferControllerTests
{
    private readonly ITransferService _transferService;
    private readonly IAccountService _accountService;
    private readonly ITransactionValidator _transactionValidator;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<TransferController> _logger;
    private readonly TransferController _controller;

    public TransferControllerTests()
    {
        _transferService = Substitute.For<ITransferService>();
        _accountService = Substitute.For<IAccountService>();
        _transactionValidator = Substitute.For<ITransactionValidator>();
        _currentUserService = Substitute.For<CurrentUserService>();
        _logger = Substitute.For<ILogger<TransferController>>();

        _controller = new TransferController(
            _transferService,
            _accountService,
            _currentUserService,
            _logger);
    }

    [Fact]
    public async Task TransferFunds_ValidRequest_ShouldReturnOk()
    {
        // Arrange
        var userId = 1;
        var request = new TransferRequest
        {
            RecipientAccountNumber = "8348753494",
            Amount = 100,
            Description = "Test transfer"
        };

        var senderAccountInfo = new AccountInfoResponse
        {
            UserId = userId,
            AccountNumber = "1234567890",
            Balance = 1000,
        };

        var senderAccount = new Account
        {
            UserId = userId,
            AccountNumber = "1234567890",
            Balance = 1000,
            IsActive = true,
            Currency = "USD"
        };

        var validationResult = new ValidationResult();
        var transferResponse = new TransferResponse
        {
            Success = true,
            Message = "Transfer successful",
            TransactionReference = "12376678",
            NewBalance = 900
        };

        _currentUserService.UserId.Returns(userId);
        _accountService.GetAccountInfoAsync(userId)
            .Returns(senderAccountInfo);
        _transactionValidator.ValidateTransferAsync(senderAccount, request.RecipientAccountNumber, request.Amount, Arg.Any<CancellationToken>())
            .Returns(validationResult);
        _transferService.TransferFundsAsync(userId, request, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(transferResponse);

        // Act
        var result = await _controller.TransferFunds(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeOfType<TransferResponse>();
    }

    [Fact]
    public async Task TransferFunds_Unauthenticated_ShouldReturnUnauthorized()
    {
        // Arrange
        var request = new TransferRequest();
        _currentUserService.UserId.Returns((int?)null);

        // Act
        var result = await _controller.TransferFunds(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task TransferFunds_ValidationFails_ShouldReturnBadRequest()
    {
        // Arrange
        var userId = 1;
        var request = new Application.DTOs.Transfer.TransferRequest
        {
            RecipientAccountNumber = "8348753494",
            Amount = 100,
            Description = "Test transfer"
        };


        var senderAccountInfo = new AccountInfoResponse
        {
            UserId = userId,
            AccountNumber = "1234567890",
            Balance = 1000,
        };

        var senderAccount = new Account
        {
            UserId = userId,
            AccountNumber = "1234567890",
            Balance = 1000,
            IsActive = true,
            Currency = "USD"
        };

        var validationResult = new ValidationResult();
        validationResult.AddError("Insufficient funds", "INSUFFICIENT_FUNDS");

        _currentUserService.UserId.Returns(userId);
        _accountService.GetAccountInfoAsync(userId)
            .Returns(senderAccountInfo);
        _transactionValidator.ValidateTransferAsync(senderAccount, request.RecipientAccountNumber, request.Amount, Arg.Any<CancellationToken>())
            .Returns(validationResult);

        // Act
        var result = await _controller.TransferFunds(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        badRequestResult!.Value.Should().BeOfType<ValidationErrorResponse>();
    }

    [Fact]
    public async Task TransferFunds_InsufficientFunds_ShouldReturnBadRequest()
    {
        // Arrange
        var userId = 1;
        var request = new TransferRequest
        {
            RecipientAccountNumber = "8348753494",
            Amount = 100,
            Description = "Test transfer"
        };

        var senderAccount = new Account
        {
            UserId = userId,
            AccountNumber = "123456",
            Balance = 50,
            IsActive = true,
            Currency = "USD"
        };
        var senderAccountInfo = new AccountInfoResponse
        {
            UserId = userId,
            AccountNumber = "1234567890",
            Balance = 1000,
        };

        var validationResult = new ValidationResult();
        validationResult.AddError("Insufficient funds", "INSUFFICIENT_FUNDS");

        _currentUserService.UserId.Returns(userId);
        _accountService.GetAccountInfoAsync(userId)
            .Returns(senderAccountInfo);
        _transactionValidator.ValidateTransferAsync(senderAccount, request.RecipientAccountNumber, request.Amount, Arg.Any<CancellationToken>())
            .Returns(validationResult);

        // Act
        var result = await _controller.TransferFunds(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        var response = badRequestResult!.Value as ValidationErrorResponse;
        response.Should().NotBeNull();
        response!.Code.Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public async Task TransferFunds_ConcurrencyConflict_ShouldReturnConflict()
    {
        // Arrange
        var userId = 1;
        var request = new TransferRequest
        {
            RecipientAccountNumber = "8348753494",
            Amount = 100,
            Description = "Test transfer"
        };

        var senderAccount = new Account
        {
            UserId = userId,
            AccountNumber = "123456",
            Balance = 1000,
            IsActive = true,
            Currency = "USD"
        };
        var senderAccountInfo = new AccountInfoResponse
        {
            UserId = userId,
            AccountNumber = "1234567890",
            Balance = 1000,
        };

        var validationResult = new ValidationResult();

        _currentUserService.UserId.Returns(userId);
        _accountService.GetAccountInfoAsync(userId)
            .Returns(senderAccountInfo);
        _transactionValidator.ValidateTransferAsync(senderAccount, request.RecipientAccountNumber, request.Amount, Arg.Any<CancellationToken>())
            .Returns(validationResult);
        _transferService.TransferFundsAsync(userId, request, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<TransferResponse>(new ConcurrencyException("Concurrency conflict")));

        // Act
        var result = await _controller.TransferFunds(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ConflictObjectResult>();
        var conflictResult = result as ConflictObjectResult;
        var response = conflictResult!.Value as ErrorResponse;
        response.Should().NotBeNull();
        response!.Code.Should().Be("CONCURRENCY_CONFLICT");
        response.Retryable.Should().BeTrue();
    }

    [Fact]
    public async Task GetTransferFee_ValidAmount_ShouldReturnFeeInfo()
    {
        // Arrange
        var amount = 100m;

        // Act
        var result = _controller.GetTransferFee(amount);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var feeInfo = okResult!.Value as FeeInfoResponse;
        feeInfo.Should().NotBeNull();
        feeInfo!.Amount.Should().Be(100);
        feeInfo.Fee.Should().Be(1); // Minimum fee of $1
        feeInfo.TotalAmount.Should().Be(101);
    }

    [Fact]
    public async Task GetTransferFee_InvalidAmount_ShouldReturnBadRequest()
    {
        // Arrange
        var amount = -100m;

        // Act
        var result = _controller.GetTransferFee(amount);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }
}