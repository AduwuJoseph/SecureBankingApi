using BankingAPI.Application.DTOs.Transaction;
using BankingAPI.Application.Interfaces;
using BankingAPI.Application.Services;
using BankingAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankingAPI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _transactionService;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<TransactionsController> _logger;

    public TransactionsController(
        ITransactionService transactionService,
        CurrentUserService currentUserService,
        ILogger<TransactionsController> logger)
    {
        _transactionService = transactionService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Get transaction by ID
    /// </summary>
    [HttpGet("{transactionId}")]
    [ProducesResponseType(typeof(TransactionViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTransactionById(int transactionId)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedAccessException();

        var result = await _transactionService.GetTransactionByIdAsync(userId, transactionId);

        if (result == null)
        {
            return NotFound(new { message = "Transaction not found" });
        }

        return Ok(result);
    }

    /// <summary>
    /// Get transaction history with pagination and filtering
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(TransactionHistoryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransactionHistory([FromQuery] TransactionHistoryRequest request)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedAccessException();

        var result = await _transactionService.GetTransactionHistoryAsync(userId, request);

        // Add cache headers
        Response.Headers["Cache-Control"] = "public, max-age=120";
        Response.Headers["Vary"] = "Accept-Encoding";

        return Ok(result);
    }

    /// <summary>
    /// Get transaction summary
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(TransactionSummary), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransactionSummary(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedAccessException();

        var result = await _transactionService.GetTransactionSummaryAsync(userId, startDate, endDate);

        // Cache for longer period since summary changes less frequently
        Response.Headers["Cache-Control"] = "public, max-age=600";

        return Ok(result);
    }

    /// <summary>
    /// Get recent transactions (for dashboard)
    /// </summary>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(IEnumerable<TransactionViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecentTransactions([FromQuery] int count = 10)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedAccessException();

        var result = await _transactionService.GetRecentTransactionsAsync(userId, Math.Min(count, 50));

        return Ok(result);
    }

    /// <summary>
    /// Export transaction history as CSV
    /// </summary>
    [HttpGet("export/csv")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportTransactionsToCsv([FromQuery] TransactionHistoryRequest request)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedAccessException();

        // Set reasonable limits for export
        request.PageSize = Math.Min(request.PageSize, 10000);
        request.Page = 1;

        var csvData = await _transactionService.ExportTransactionsToCsvAsync(userId, request);

        return File(csvData, "text/csv", $"transactions_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
    }

    /// <summary>
    /// Invalidate transaction cache for current user
    /// </summary>
    [HttpPost("cache/invalidate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> InvalidateCache()
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedAccessException();

        await _transactionService.InvalidateTransactionCacheAsync(userId);

        return Ok(new { message = "Cache invalidated successfully" });
    }

    /// <summary>
    /// Get pagination metadata helper
    /// </summary>
    [HttpGet("pagination-info")]
    [ProducesResponseType(typeof(PaginationMetadata), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaginationInfo([FromQuery] TransactionHistoryRequest request)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedAccessException();

        var result = await _transactionService.GetTransactionHistoryAsync(userId, request);

        return Ok(result.Pagination);
    }
}