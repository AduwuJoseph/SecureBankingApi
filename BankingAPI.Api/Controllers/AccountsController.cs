using BankingAPI.Application.DTOs.Account;
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
public class AccountController : ControllerBase
{
    private readonly IAccountService _accountService;
    private readonly CurrentUserService _currentUserService;

    public AccountController(IAccountService accountService, CurrentUserService currentUserService)
    {
        _accountService = accountService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Get current user's account information
    /// </summary>
    [HttpGet("current-user")]
    [ProducesResponseType(typeof(AccountInfoResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAccountInfo()
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
        var result = await _accountService.GetAccountInfoAsync(userId);
        return Ok(result);
    }

    /// <summary>
    /// Update account contact information
    /// </summary>
    [HttpPut("update-contact")]
    [ProducesResponseType(typeof(AccountInfoResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateContactInfo([FromBody] AccountUpdateRequest updateDto)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
        var result = await _accountService.UpdateContactInfoAsync(userId, updateDto);
        return Ok(result);
    }

    /// <summary>
    /// Get transaction history with pagination and filtering
    /// </summary>
    [HttpGet("transactions")]
    [ProducesResponseType(typeof(TransactionHistoryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransactionHistory([FromQuery] TransactionHistoryRequest request)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
        var result = await _accountService.GetTransactionHistoryAsync(userId, request);
        return Ok(result);
    }

    /// <summary>
    /// Get transaction by ID
    /// </summary>
    [HttpGet("transactions/{transactionId}")]
    [ProducesResponseType(typeof(TransactionViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTransactionById(int transactionId)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
        var result = await _accountService.GetTransactionByIdAsync(userId, transactionId);

        if (result == null)
        {
            return NotFound(new { message = "Transaction not found" });
        }

        return Ok(result);
    }

    /// <summary>
    /// Export transaction history as CSV
    /// </summary>
    [HttpGet("transactions/export")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportTransactions([FromQuery] TransactionHistoryRequest request)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
        request.PageSize = 10000; // Max limit for export
        var result = await _accountService.GetTransactionHistoryAsync(userId, request);

        var csv = GenerateTransactionCsv(result.Transactions);
        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);

        return File(bytes, "text/csv", $"transactions_{DateTime.Now:yyyyMMdd}.csv");
    }

    private string GenerateTransactionCsv(List<TransactionViewModel> transactions)
    {
        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Transaction ID,Reference,Type,Status,Amount,Description,Counterparty,Date,Time");

        foreach (var transaction in transactions)
        {
            csv.AppendLine($"{transaction.TransactionId},{transaction.Reference},{transaction.Type}," +
                          $"{transaction.Status},{transaction.Amount},{transaction.Description}," +
                          $"{transaction.Counterparty.Name},{transaction.FormattedDate},{transaction.FormattedTime}");
        }

        return csv.ToString();
    }
}