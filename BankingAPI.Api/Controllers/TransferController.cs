using BankingAPI.Api.Extensions;
using BankingAPI.Application.DTOs.Errors;
using BankingAPI.Application.DTOs.Transaction;
using BankingAPI.Application.DTOs.Transfer;
using BankingAPI.Application.Interfaces;
using BankingAPI.Application.Services;
using BankingAPI.Domain.Entities;
using BankingAPI.Domain.Exceptions;
using BankingAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;

namespace BankingAPI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class TransferController : ControllerBase
{
    private readonly ITransferService _transferService;
    private readonly IAccountService _accountService;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<TransferController> _logger;

    public TransferController(
        ITransferService transferService,
        IAccountService accountService,
        CurrentUserService currentUserService,
        ILogger<TransferController> logger)
    {
        _transferService = transferService;
        _accountService = accountService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Transfer funds to another user
    /// </summary>
    /// <param name="request">Transfer request details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Transfer response with transaction details</returns>
    [HttpPost]
    [EnableRateLimiting("TransferPolicy")]
    [ProducesResponseType(typeof(TransferResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> TransferFunds(
        [FromBody] TransferRequest request,
        CancellationToken cancellationToken)
    {
        // Get current user
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "User not authenticated",
                Code = "UNAUTHORIZED"
            });
        }

        // Validate request
        if (!ModelState.IsValid)
        {
            return BadRequest(new ValidationErrorResponse
            {
                Message = "Invalid transfer request",
                Errors = ModelState.GetErrorMessages()
            });
        }

        // Get idempotency key from header
        var idempotencyKey = GetIdempotencyKey();

        // Validate request
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            return BadRequest(new ValidationErrorResponse
            {
                Message = "Invalid transfer request",
                Errors = new List<string> { "Idempotency key [Idempotency-Key] is required as a header" }
            });
        }

        // Process the transfer
        var result = await _transferService.TransferFundsAsync(
            userId.Value,
            request,
            idempotencyKey,
            cancellationToken);

        // Return success response
        return Ok(result);
    }

    /// <summary>
    /// Get transfer fee information
    /// </summary>
    /// <param name="amount">Transfer amount</param>
    /// <returns>Fee information</returns>
    [HttpGet("fee")]
    [ProducesResponseType(typeof(FeeInfoResponse), StatusCodes.Status200OK)]
    public IActionResult GetTransferFee([FromQuery] decimal amount)
    {
        if (amount <= 0)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "Amount must be greater than zero",
                Code = "INVALID_AMOUNT"
            });
        }

        // Calculate fee based on amount (example: 0.5% with min $1 and max $25)
        var fee = CalculateFee(amount);
        var totalAmount = amount + fee;

        return Ok(new FeeInfoResponse
        {
            Amount = amount,
            Fee = fee,
            TotalAmount = totalAmount,
            FeePercentage = 0.5m,
            FeeDescription = "Transaction fee (0.5% with min $1 and max $25)"
        });
    }

    /// <summary>
    /// Get transfer limits
    /// </summary>
    /// <returns>Transfer limits information</returns>
    [HttpGet("limits")]
    [ProducesResponseType(typeof(TransferLimitsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransferLimits(CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "User not authenticated",
                Code = "UNAUTHORIZED"
            });
        }

        var dailyTotal = await _transferService.GetDailyTransferTotalAsync(userId.Value, cancellationToken);
        var dailyLimit = 25000m; // Should come from configuration
        var remainingDaily = Math.Max(0, dailyLimit - dailyTotal);

        return Ok(new TransferLimitsResponse
        {
            MinimumAmount = 0.01m,
            MaximumAmount = 10000m,
            DailyLimit = dailyLimit,
            DailyUsed = dailyTotal,
            DailyRemaining = remainingDaily,
            Currency = "USD"
        });
    }

    #region Private Helper Methods

    private string GetIdempotencyKey()
    {
        // Check header first
        if (Request.Headers.TryGetValue("Idempotency-Key", out var keyValues))
        {
            var key = keyValues.ToString();
            if (!string.IsNullOrWhiteSpace(key))
            {
                return key;
            }
        }
        return string.Empty;
    }


    private decimal CalculateFee(decimal amount)
    {
        var fee = amount * 0.005m; // 0.5%
        fee = Math.Max(fee, 1m); // Minimum $1
        fee = Math.Min(fee, 25m); // Maximum $25
        return fee;
    }

    private string MaskAccountNumber(string accountNumber)
    {
        if (string.IsNullOrEmpty(accountNumber) || accountNumber.Length < 8)
            return "****";

        return $"{accountNumber[..4]}****{accountNumber[^4..]}";
    }

    #endregion
}