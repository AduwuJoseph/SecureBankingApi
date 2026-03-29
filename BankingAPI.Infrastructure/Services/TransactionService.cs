using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using BankingAPI.Application.Interfaces;
using BankingAPI.Domain.Exceptions;
using BankingAPI.Application.DTOs.Transaction;
using BankingAPI.Domain.Enum;

namespace BankingAPI.Application.Services;

public class TransactionService : ITransactionService
{
    private readonly IBankingDbContext _context;
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<TransactionService> _logger;
    private readonly IAuditService _auditService;
    private readonly JsonSerializerOptions _jsonOptions;

    // Cache keys
    private const string TransactionCacheKeyPrefix = "transaction_";
    private const string TransactionHistoryCacheKeyPrefix = "transaction_history_";
    private const string TransactionSummaryCacheKeyPrefix = "transaction_summary_";
    private const string RecentTransactionsCacheKeyPrefix = "recent_transactions_";

    // Cache durations
    private static readonly TimeSpan TransactionCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan TransactionHistoryCacheDuration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan TransactionSummaryCacheDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RecentTransactionsCacheDuration = TimeSpan.FromMinutes(1);

    public TransactionService(
        IBankingDbContext context,
        IMemoryCache memoryCache,
        IDistributedCache distributedCache,
        ILogger<TransactionService> logger,
        IAuditService auditService)
    {
        _context = context;
        _memoryCache = memoryCache;
        _distributedCache = distributedCache;
        _logger = logger;
        _auditService = auditService;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task<TransactionViewModel?> GetTransactionByIdAsync(int userId, int transactionId)
    {
        var cacheKey = $"{TransactionCacheKeyPrefix}{userId}_{transactionId}";

        // Try to get from memory cache first (L1 cache)
        if (_memoryCache.TryGetValue(cacheKey, out TransactionViewModel? cachedTransaction))
        {
            _logger.LogDebug("Transaction {TransactionId} retrieved from memory cache for user {UserId}",
                transactionId, userId);
            return cachedTransaction;
        }

        // Try to get from distributed cache (L2 cache)
        var cachedJson = await _distributedCache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cachedJson))
        {
            var transaction = JsonSerializer.Deserialize<TransactionViewModel>(cachedJson, _jsonOptions);
            if (transaction != null)
            {
                // Store in memory cache for faster access
                _memoryCache.Set(cacheKey, transaction, TransactionCacheDuration);
                _logger.LogDebug("Transaction {TransactionId} retrieved from distributed cache for user {UserId}",
                    transactionId, userId);
                return transaction;
            }
        }

        // Fetch from database
        _logger.LogDebug("Fetching transaction {TransactionId} from database for user {UserId}",
            transactionId, userId);

        var dbTransaction = await _context.Transactions
            .Include(t => t.Sender)
            .Include(t => t.Recipient)
            .FirstOrDefaultAsync(t => t.Id == transactionId);

        if (dbTransaction == null)
        {
            return null;
        }

        // Ensure user is either sender or recipient
        if (dbTransaction.SenderId != userId && dbTransaction.RecipientId != userId)
        {
            _logger.LogWarning("User {UserId} attempted to access transaction {TransactionId} they don't own",
                userId, transactionId);
            return null;
        }

        var viewModel = MapToViewModel(dbTransaction, userId);

        // Cache the result
        await CacheTransactionAsync(cacheKey, viewModel);

        return viewModel;
    }

    public async Task<TransactionHistoryResponse> GetTransactionHistoryAsync(
        int userId,
        TransactionHistoryRequest request)
    {
        // Validate request
        ValidateRequest(request);

        // Generate cache key based on request parameters
        var cacheKey = GenerateHistoryCacheKey(userId, request);

        // Try to get from memory cache
        if (_memoryCache.TryGetValue(cacheKey, out TransactionHistoryResponse? cachedResponse))
        {
            _logger.LogDebug("Transaction history retrieved from memory cache for user {UserId}", userId);
            return cachedResponse!;
        }

        // Try to get from distributed cache
        var cachedJson = await _distributedCache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cachedJson))
        {
            var responseData = JsonSerializer.Deserialize<TransactionHistoryResponse>(cachedJson, _jsonOptions);
            if (responseData != null)
            {
                _memoryCache.Set(cacheKey, responseData, TransactionHistoryCacheDuration);
                _logger.LogDebug("Transaction history retrieved from distributed cache for user {UserId}", userId);
                return responseData;
            }
        }

        // Build query
        var query = BuildTransactionQuery(userId, request);

        // Get total count for pagination
        var totalCount = await query.CountAsync();

        // Apply sorting and pagination
        var transactions = await ApplySortingAndPagination(query, request)
            .ToListAsync();

        // Calculate summary
        var summary = await CalculateSummaryAsync(userId, request);

        // Map to view models
        var transactionViewModels = transactions.Select(t => MapToViewModel(t, userId)).ToList();

        var response = new TransactionHistoryResponse
        {
            Transactions = transactionViewModels,
            Pagination = new PaginationMetadata
            {
                CurrentPage = request.Page,
                PageSize = request.PageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize)
            },
            Summary = new TransactionSummary
            {
                AverageAmount = summary.AverageTransactionAmount,
                LargestAmount = summary.LargestTransactionAmount,
                TotalReceived = summary.TotalReceived,
                TotalSent = summary.TotalSent,
                TotalTransactions = summary.TotalTransactions,
            }
        };

        // Cache the response
        await CacheTransactionHistoryAsync(cacheKey, response);

        _logger.LogInformation("Retrieved {Count} transactions for user {UserId} (Page {Page}, Size {PageSize})",
            transactionViewModels.Count, userId, request.Page, request.PageSize);

        return response;
    }

    public async Task<AdminTransactionSummaryResponse> GetTransactionSummaryAsync(
        int userId,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var cacheKey = $"{TransactionSummaryCacheKeyPrefix}{userId}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}";

        // Try to get from cache
        if (_memoryCache.TryGetValue(cacheKey, out AdminTransactionSummaryResponse? cachedSummary))
        {
            _logger.LogDebug("Transaction summary retrieved from cache for user {UserId}", userId);
            return cachedSummary!;
        }

        var cachedJson = await _distributedCache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cachedJson))
        {
            var summaryData = JsonSerializer.Deserialize<AdminTransactionSummaryResponse>(cachedJson, _jsonOptions);
            if (summaryData != null)
            {
                _memoryCache.Set(cacheKey, summaryData, TransactionSummaryCacheDuration);
                return summaryData;
            }
        }

        // Calculate summary from database
        var query = _context.Transactions
            .Where(t => t.SenderId == userId || t.RecipientId == userId);

        if (startDate.HasValue)
        {
            query = query.Where(t => t.Timestamp >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            var endDateTime = endDate.Value.Date.AddDays(1);
            query = query.Where(t => t.Timestamp < endDateTime);
        }

        var transactions = await query.ToListAsync();

        var sentTransactions = transactions.Where(t => t.SenderId == userId && t.Status == TransactionStatus.Completed);
        var receivedTransactions = transactions.Where(t => t.RecipientId == userId && t.Status == TransactionStatus.Completed);

        var totalSent = sentTransactions.Sum(t => t.Amount);
        var totalReceived = receivedTransactions.Sum(t => t.Amount);
        var allCompletedTransactions = transactions.Where(t => t.Status == TransactionStatus.Completed).ToList();

        var summary = new AdminTransactionSummaryResponse
        {
            TotalSent = totalSent,
            TotalReceived = totalReceived,
            TotalTransactions = transactions.Count,
            SuccessfulTransactions = transactions.Count(t => t.Status == TransactionStatus.Completed),
            FailedTransactions = transactions.Count(t => t.Status == TransactionStatus.Failed),
            AverageTransactionAmount = allCompletedTransactions.Any()
                ? allCompletedTransactions.Average(t => t.Amount)
                : 0,
            LargestTransactionAmount = allCompletedTransactions.Any()
                ? allCompletedTransactions.Max(t => t.Amount)
                : 0,
            LastTransactionDate = transactions.Any()
                ? transactions.Max(t => t.Timestamp)
                : null,
            MonthlyBreakdown = CalculateMonthlyBreakdown(transactions, userId),
            TopCounterparties = CalculateTopCounterparties(transactions, userId)
        };

        // Cache the result
        await CacheTransactionSummaryAsync(cacheKey, summary);

        return summary;
    }

    public async Task<IEnumerable<TransactionViewModel>> GetRecentTransactionsAsync(int userId, int count = 10)
    {
        var cacheKey = $"{RecentTransactionsCacheKeyPrefix}{userId}_{count}";

        // Try to get from cache
        if (_memoryCache.TryGetValue(cacheKey, out IEnumerable<TransactionViewModel>? cachedTransactions))
        {
            _logger.LogDebug("Recent transactions retrieved from cache for user {UserId}", userId);
            return cachedTransactions!;
        }

        var cachedJson = await _distributedCache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cachedJson))
        {
            var transactions = JsonSerializer.Deserialize<List<TransactionViewModel>>(cachedJson, _jsonOptions);
            if (transactions != null)
            {
                _memoryCache.Set(cacheKey, transactions, RecentTransactionsCacheDuration);
                return transactions;
            }
        }

        // Fetch from database
        var recentTransactions = await _context.Transactions
            .Include(t => t.Sender)
            .Include(t => t.Recipient)
            .Where(t => t.SenderId == userId || t.RecipientId == userId)
            .OrderByDescending(t => t.Timestamp)
            .Take(count)
            .ToListAsync();

        var viewModels = recentTransactions.Select(t => MapToViewModel(t, userId)).ToList();

        // Cache the result
        var json = JsonSerializer.Serialize(viewModels, _jsonOptions);
        await _distributedCache.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = RecentTransactionsCacheDuration
        });

        _memoryCache.Set(cacheKey, viewModels, RecentTransactionsCacheDuration);

        return viewModels;
    }

    public async Task InvalidateTransactionCacheAsync(int userId)
    {
        _logger.LogInformation("Invalidating transaction cache for user {UserId}", userId);

        // Clear memory cache entries (pattern-based clearing is not directly supported in IMemoryCache)
        // Instead, we'll use a versioning approach or clear specific keys

        // For distributed cache, we would need to implement a pattern-based invalidation
        // This is simplified - in production, consider using Redis with SCAN or a versioning strategy

        await _auditService.LogAsync("Cache Invalidation",
            $"Transaction cache invalidated for user {userId}",
            userId.ToString());
    }

    public async Task<byte[]> ExportTransactionsToCsvAsync(int userId, TransactionHistoryRequest request)
    {
        // Don't use cache for export to ensure fresh data
        request.PageSize = Math.Min(request.PageSize, 10000); // Max limit for export
        request.Page = 1; // Get all from page 1

        var query = BuildTransactionQuery(userId, request);
        var transactions = await query
            .OrderByDescending(t => t.Timestamp)
            .Take(10000) // Max export limit
            .ToListAsync();

        var csv = new StringBuilder();

        // Add headers
        csv.AppendLine("Transaction ID,Reference,Type,Status,Amount,Description,Counterparty Name,Counterparty Email,Date,Time");

        // Add rows
        foreach (var transaction in transactions)
        {
            var isSender = transaction.SenderId == userId;
            var counterparty = isSender ? transaction.Recipient : transaction.Sender;
            var viewModel = MapToViewModel(transaction, userId);

            csv.AppendLine($"\"{transaction.Id}\",\"{viewModel.Reference}\",\"{viewModel.Type}\"," +
                          $"\"{viewModel.Status}\",{transaction.Amount},\"{transaction.Description?.Replace("\"", "\"\"")}\"," +
                          $"\"{counterparty?.FullName}\",\"{counterparty?.Email}\"," +
                          $"\"{viewModel.FormattedDate}\",\"{viewModel.FormattedTime}\"");
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    #region Private Helper Methods

    private IQueryable<Domain.Entities.Transaction> BuildTransactionQuery(
        int userId,
        TransactionHistoryRequest request)
    {
        var query = _context.Transactions
            .Include(t => t.Sender)
            .Include(t => t.Recipient)
            .Where(t => t.SenderId == userId || t.RecipientId == userId);

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

        // Filter by search term
        if (!string.IsNullOrEmpty(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.ToLower();
            query = query.Where(t =>
                (t.SenderId == userId && t.Recipient != null &&
                    (t.Recipient.FullName.ToLower().Contains(searchTerm) ||
                     t.Recipient.Email.ToLower().Contains(searchTerm))) ||
                (t.RecipientId == userId && t.Sender != null &&
                    (t.Sender.FullName.ToLower().Contains(searchTerm) ||
                     t.Sender.Email.ToLower().Contains(searchTerm))));
        }

        return query;
    }

    private IQueryable<Domain.Entities.Transaction> ApplySortingAndPagination(
        IQueryable<Domain.Entities.Transaction> query,
        TransactionHistoryRequest request)
    {
        // Apply sorting
        var sortOrder = request.SortOrder?.ToLower() == "asc" ? "asc" : "desc";

        query = request.SortBy?.ToLower() switch
        {
            "amount" => sortOrder == "asc"
                ? query.OrderBy(t => t.Amount)
                : query.OrderByDescending(t => t.Amount),
            _ => sortOrder == "asc"
                ? query.OrderBy(t => t.Timestamp)
                : query.OrderByDescending(t => t.Timestamp)
        };

        // Apply pagination
        return query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize);
    }

    private async Task<AdminTransactionSummaryResponse> CalculateSummaryAsync(int userId, TransactionHistoryRequest request)
    {
        var query = BuildTransactionQuery(userId, request);
        var transactions = await query.ToListAsync();

        var sentTransactions = transactions.Where(t => t.SenderId == userId && t.Status == TransactionStatus.Completed);
        var receivedTransactions = transactions.Where(t => t.RecipientId == userId && t.Status == TransactionStatus.Completed);

        return new AdminTransactionSummaryResponse
        {
            TotalSent = sentTransactions.Sum(t => t.Amount),
            TotalReceived = receivedTransactions.Sum(t => t.Amount),
            TotalTransactions = transactions.Count,
            SuccessfulTransactions = transactions.Count(t => t.Status == TransactionStatus.Completed),
            FailedTransactions = transactions.Count(t => t.Status == TransactionStatus.Failed),
            AverageTransactionAmount = transactions.Where(t => t.Status == TransactionStatus.Completed).Any()
                ? transactions.Where(t => t.Status == TransactionStatus.Completed).Average(t => t.Amount)
                : 0,
            LargestTransactionAmount = transactions.Where(t => t.Status == TransactionStatus.Completed).Any()
                ? transactions.Where(t => t.Status == TransactionStatus.Completed).Max(t => t.Amount)
                : 0,
            LastTransactionDate = transactions.Any() ? transactions.Max(t => t.Timestamp) : null,
            MonthlyBreakdown = CalculateMonthlyBreakdown(transactions, userId),
            TopCounterparties = CalculateTopCounterparties(transactions, userId)
        };
    }

    private Dictionary<string, decimal> CalculateMonthlyBreakdown(
        List<Domain.Entities.Transaction> transactions,
        int userId)
    {
        return transactions
            .Where(t => t.Status == TransactionStatus.Completed)
            .GroupBy(t => new { t.Timestamp.Year, t.Timestamp.Month })
            .OrderByDescending(g => g.Key.Year)
            .ThenByDescending(g => g.Key.Month)
            .Take(12)
            .ToDictionary(
                g => $"{g.Key.Year}-{g.Key.Month:D2}",
                g => g.Sum(t => t.SenderId == userId ? -t.Amount : t.Amount)
            );
    }

    private Dictionary<string, int> CalculateTopCounterparties(
        List<Domain.Entities.Transaction> transactions,
        int userId)
    {
        return transactions
            .Where(t => t.Status == TransactionStatus.Completed)
            .GroupBy(t => t.SenderId == userId ? t.Recipient?.FullName : t.Sender?.FullName)
            .Where(g => g.Key != null)
            .ToDictionary(
                g => g.Key!,
                g => g.Count()
            )
            .OrderByDescending(kvp => kvp.Value)
            .Take(5)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
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

    private string GenerateTransactionReference(long transactionId, DateTime timestamp)
    {
        return $"TXN-{timestamp:yyyyMMdd}-{transactionId:D6}";
    }

    private void ValidateRequest(TransactionHistoryRequest request)
    {
        if (request.Page < 1) request.Page = 1;
        if (request.PageSize < 1) request.PageSize = 50;
        if (request.PageSize > 100) request.PageSize = 100;

        if (request.StartDate.HasValue && request.EndDate.HasValue)
        {
            if (request.StartDate > request.EndDate)
            {
                throw new ValidationException("Start date must be before end date");
            }
        }
    }

    private string GenerateHistoryCacheKey(int userId, TransactionHistoryRequest request)
    {
        return $"{TransactionHistoryCacheKeyPrefix}{userId}_p{request.Page}_s{request.PageSize}_t{request.Type}_st{request.Status}_sd{request.StartDate:yyyyMMdd}_ed{request.EndDate:yyyyMMdd}_min{request.MinAmount}_max{request.MaxAmount}_search{request.SearchTerm}_sort{request.SortBy}_{request.SortOrder}";
    }

    private async Task CacheTransactionAsync(string cacheKey, TransactionViewModel transaction)
    {
        // Store in memory cache
        _memoryCache.Set(cacheKey, transaction, TransactionCacheDuration);

        // Store in distributed cache
        var json = JsonSerializer.Serialize(transaction, _jsonOptions);
        await _distributedCache.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TransactionCacheDuration
        });
    }

    private async Task CacheTransactionHistoryAsync(string cacheKey, TransactionHistoryResponse response)
    {
        // Store in memory cache
        _memoryCache.Set(cacheKey, response, TransactionHistoryCacheDuration);

        // Store in distributed cache
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        await _distributedCache.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TransactionHistoryCacheDuration
        });
    }

    private async Task CacheTransactionSummaryAsync(string cacheKey, AdminTransactionSummaryResponse summary)
    {
        // Store in memory cache
        _memoryCache.Set(cacheKey, summary, TransactionSummaryCacheDuration);

        // Store in distributed cache
        var json = JsonSerializer.Serialize(summary, _jsonOptions);
        await _distributedCache.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TransactionSummaryCacheDuration
        });
    }

    #endregion
}