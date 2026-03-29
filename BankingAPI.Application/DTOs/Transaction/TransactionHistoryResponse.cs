using System.Text.Json.Serialization;

namespace BankingAPI.Application.DTOs.Transaction;

/// <summary>
/// Response DTO for transaction history with pagination
/// </summary>
public class TransactionHistoryResponse
{
    /// <summary>
    /// List of transactions
    /// </summary>
    [JsonPropertyName("transactions")]
    public List<TransactionViewModel> Transactions { get; set; } = new();

    /// <summary>
    /// Pagination metadata
    /// </summary>
    [JsonPropertyName("pagination")]
    public PaginationMetadata Pagination { get; set; } = new();

    /// <summary>
    /// Summary statistics
    /// </summary>
    [JsonPropertyName("summary")]
    public TransactionSummary Summary { get; set; } = new();
}

/// <summary>
/// Pagination metadata
/// </summary>
public class PaginationMetadata
{
    /// <summary>
    /// Current page number
    /// </summary>
    [JsonPropertyName("currentPage")]
    public int CurrentPage { get; set; } = 1;

    /// <summary>
    /// Number of items per page
    /// </summary>
    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; } = 50;

    /// <summary>
    /// Total number of items
    /// </summary>
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    /// <summary>
    /// Total number of pages
    /// </summary>
    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }

    /// <summary>
    /// Has previous page
    /// </summary>
    [JsonPropertyName("hasPrevious")]
    public bool HasPrevious => CurrentPage > 1;

    /// <summary>
    /// Has next page
    /// </summary>
    [JsonPropertyName("hasNext")]
    public bool HasNext => CurrentPage < TotalPages;
}

/// <summary>
/// Transaction summary statistics
/// </summary>
public class TransactionSummary
{
    /// <summary>
    /// Total number of transactions
    /// </summary>
    [JsonPropertyName("totalTransactions")]
    public int TotalTransactions { get; set; }

    /// <summary>
    /// Total amount sent
    /// </summary>
    [JsonPropertyName("totalSent")]
    public decimal TotalSent { get; set; }

    /// <summary>
    /// Total amount received
    /// </summary>
    [JsonPropertyName("totalReceived")]
    public decimal TotalReceived { get; set; }

    /// <summary>
    /// Net flow (received - sent)
    /// </summary>
    [JsonPropertyName("netFlow")]
    public decimal NetFlow => TotalReceived - TotalSent;

    /// <summary>
    /// Average transaction amount
    /// </summary>
    [JsonPropertyName("averageAmount")]
    public decimal AverageAmount { get; set; }

    /// <summary>
    /// Largest transaction amount
    /// </summary>
    [JsonPropertyName("largestAmount")]
    public decimal LargestAmount { get; set; }

    /// <summary>
    /// Formatted total sent
    /// </summary>
    [JsonPropertyName("formattedTotalSent")]
    public string FormattedTotalSent => $"{TotalSent:C2}";

    /// <summary>
    /// Formatted total received
    /// </summary>
    [JsonPropertyName("formattedTotalReceived")]
    public string FormattedTotalReceived => $"{TotalReceived:C2}";

    /// <summary>
    /// Formatted net flow
    /// </summary>
    [JsonPropertyName("formattedNetFlow")]
    public string FormattedNetFlow => $"{(NetFlow >= 0 ? "+" : "-")}{Math.Abs(NetFlow):C2}";
}