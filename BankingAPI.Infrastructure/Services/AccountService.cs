using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BankingAPI.Application.DTOs.Account;
using BankingAPI.Application.DTOs.Transaction;
using BankingAPI.Application.Interfaces;
using BankingAPI.Domain.Enum;
using BankingAPI.Domain.Exceptions;
using System.Linq.Expressions;

namespace BankingAPI.Infrastructure.Services;

public class AccountService : IAccountService
{
    private readonly IBankingDbContext _context;
    private readonly ILogger<AccountService> _logger;
    private readonly IAuditService _auditService;

    public AccountService(
        IBankingDbContext context,
        ILogger<AccountService> logger,
        IAuditService auditService)
    {
        _context = context;
        _logger = logger;
        _auditService = auditService;
    }

    public async Task<AccountInfoResponse?> GetAccountInfoAsync(int userId)
    {
        var user = await _context.Users
            .Include(u => u.Account)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            throw new NotFoundException($"User with id {userId} not found");
        }

        return new AccountInfoResponse
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            AccountNumber = user.Account?.AccountNumber,
            Balance = user.Account?.Balance ?? 0,
            AccountCreated = user.CreatedAt,
            LastUpdated = user.UpdatedAt
        };
    }

    public async Task<AccountInfoResponse?> UpdateContactInfoAsync(int userId, AccountUpdateRequest updateDto)
    {
        var user = await _context.Users
            .Include(u => u.Account)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            throw new NotFoundException($"User with id {userId} not found");
        }

        user.FullName = updateDto.FullName;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} updated contact info", userId);
        await _auditService.LogAsync("Update Contact", $"User {userId} updated name to {updateDto.FullName}", userId.ToString());

        return new AccountInfoResponse
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            AccountNumber = user.Account?.AccountNumber,
            Balance = user.Account?.Balance ?? 0,
            AccountCreated = user.CreatedAt,
            LastUpdated = user.UpdatedAt
        };
    }

    public async Task<TransactionHistoryResponse> GetTransactionHistoryAsync(
        int userId,
        TransactionHistoryRequest request)
    {
        // Validate request
        if (request.Page < 1) request.Page = 1;
        if (request.PageSize < 1) request.PageSize = 50;
        if (request.PageSize > 100) request.PageSize = 100;

        // Build query
        var query = _context.Transactions
            .Include(t => t.Sender)
            .Include(t => t.Recipient)
            .Where(t => t.SenderId == userId || t.RecipientId == userId);

        // Apply filters
        query = ApplyFilters(query, userId, request);

        // Get total count for pagination
        var totalCount = await query.CountAsync();

        // Apply sorting
        query = ApplySorting(query, request);

        // Apply pagination
        var transactions = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        // Map to view models
        var transactionViewModels = transactions.Select(t => MapToViewModel(t, userId)).ToList();

        // Calculate summary
        var summary = CalculateSummary(transactions, userId);

        return new TransactionHistoryResponse
        {
            Transactions = transactionViewModels,
            Pagination = new PaginationMetadata
            {
                CurrentPage = request.Page,
                PageSize = request.PageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize)
            },
            Summary = summary
        };
    }

    public async Task<TransactionViewModel?> GetTransactionByIdAsync(int userId, int transactionId)
    {
        var transaction = await _context.Transactions
            .Include(t => t.Sender)
            .Include(t => t.Recipient)
            .FirstOrDefaultAsync(t => t.Id == transactionId);

        if (transaction == null)
        {
            return null;
        }

        // Ensure user is either sender or recipient
        if (transaction.SenderId != userId && transaction.RecipientId != userId)
        {
            return null;
        }

        return MapToViewModel(transaction, userId);
    }

    private IQueryable<Domain.Entities.Transaction> ApplyFilters(
        IQueryable<Domain.Entities.Transaction> query,
        int userId,
        TransactionHistoryRequest request)
    {
        // Filter by transaction type
        if (!string.IsNullOrEmpty(request.Type))
        {
            if (request.Type.Equals("sent", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(t => t.SenderId == userId);
            }
            else if (request.Type.Equals("received", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(t => t.RecipientId == userId);
            }
        }

        // Filter by status
        if (request.Status.HasValue)
        {
            query = query.Where(t => t.Status == request.Status.Value);
        }

        // Filter by date range
        if (request.StartDate.HasValue)
        {
            query = query.Where(t => t.Timestamp >= request.StartDate.Value);
        }
        if (request.EndDate.HasValue)
        {
            var endDate = request.EndDate.Value.Date.AddDays(1);
            query = query.Where(t => t.Timestamp < endDate);
        }

        // Filter by amount range
        if (request.MinAmount.HasValue)
        {
            query = query.Where(t => t.Amount >= request.MinAmount.Value);
        }
        if (request.MaxAmount.HasValue)
        {
            query = query.Where(t => t.Amount <= request.MaxAmount.Value);
        }

        // Filter by search term (counterparty name or email)
        if (!string.IsNullOrEmpty(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.ToLower();
            query = query.Where(t =>
                (t.SenderId == userId && (t.Recipient != null &&
                    (t.Recipient.FullName.ToLower().Contains(searchTerm) ||
                     t.Recipient.Email.ToLower().Contains(searchTerm)))) ||
                (t.RecipientId == userId && (t.Sender != null &&
                    (t.Sender.FullName.ToLower().Contains(searchTerm) ||
                     t.Sender.Email.ToLower().Contains(searchTerm)))));
        }

        return query;
    }

    private IQueryable<Domain.Entities.Transaction> ApplySorting(
        IQueryable<Domain.Entities.Transaction> query,
        TransactionHistoryRequest request)
    {
        var sortOrder = request.SortOrder?.ToLower() == "asc" ? "asc" : "desc";

        return request.SortBy?.ToLower() switch
        {
            "amount" => sortOrder == "asc"
                ? query.OrderBy(t => t.Amount)
                : query.OrderByDescending(t => t.Amount),
            _ => sortOrder == "asc"
                ? query.OrderBy(t => t.Timestamp)
                : query.OrderByDescending(t => t.Timestamp)
        };
    }

    private TransactionViewModel MapToViewModel(Domain.Entities.Transaction transaction, int userId)
    {
        var isSender = transaction.SenderId == userId;
        var counterparty = isSender ? transaction.Recipient : transaction.Sender;

        return new TransactionViewModel
        {
            TransactionId = transaction.Id,
            Reference = GenerateTransactionReference(transaction.Id, transaction.Timestamp),
            Type = isSender ? "Sent" : "Received",
            Status = transaction.Status.ToString(),
            StatusCode = transaction.Status,
            Amount = transaction.Amount,
            Description = transaction.Description,
            Timestamp = transaction.Timestamp,
            Category = "Transfer",
            Counterparty = new CounterpartyInfo
            {
                UserId = counterparty?.Id ?? 0,
                Name = counterparty?.FullName ?? "Unknown",
                Email = counterparty?.Email ?? "unknown@unknown.com"
            },
            Metadata = new TransactionMetadata
            {
                IdempotencyKey = transaction.IdempotencyKey,
                FailureReason = transaction.FailureReason
            }
        };
    }

    private TransactionSummary CalculateSummary(
        List<Domain.Entities.Transaction> transactions,
        int userId)
    {
        var sentTransactions = transactions.Where(t => t.SenderId == userId).ToList();
        var receivedTransactions = transactions.Where(t => t.RecipientId == userId).ToList();

        var totalSent = sentTransactions.Sum(t => t.Amount);
        var totalReceived = receivedTransactions.Sum(t => t.Amount);
        var allTransactions = transactions.Where(t => t.Status == TransactionStatus.Completed).ToList();
        var averageAmount = allTransactions.Any() ? allTransactions.Average(t => t.Amount) : 0;
        var largestAmount = allTransactions.Any() ? allTransactions.Max(t => t.Amount) : 0;

        return new TransactionSummary
        {
            TotalTransactions = transactions.Count,
            TotalSent = totalSent,
            TotalReceived = totalReceived,
            AverageAmount = averageAmount,
            LargestAmount = largestAmount
        };
    }

    private string GenerateTransactionReference(long transactionId, DateTime timestamp)
    {
        return $"TXN-{timestamp:yyyyMMdd}-{transactionId:D6}";
    }

    public async Task<AccountInfoResponse?> GetAccountWithAccountNumberAsync(string accountNumber)
    {
        var user = await _context.Users
            .Include(u => u.Account)
            .FirstOrDefaultAsync(u => u.Account.AccountNumber == accountNumber);
        if (user == null)
        {
            throw new NotFoundException($"User with account number {accountNumber} not found");
        }

        return new AccountInfoResponse
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            AccountNumber = user.Account?.AccountNumber,
            Balance = user.Account?.Balance ?? 0,
            AccountCreated = user.CreatedAt,
            LastUpdated = user.UpdatedAt
        };
    }
}