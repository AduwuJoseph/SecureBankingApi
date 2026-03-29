using System.Text.Json.Serialization;
using BankingAPI.Domain.Enum;

namespace BankingAPI.Application.DTOs.Transaction;

/// <summary>
/// ViewModel for transaction history response
/// </summary>
public class TransactionViewModel
{
    /// <summary>
    /// Unique transaction identifier
    /// </summary>
    [JsonPropertyName("transactionId")]
    public long TransactionId { get; set; }

    /// <summary>
    /// Transaction reference number (formatted for display)
    /// </summary>
    [JsonPropertyName("reference")]
    public string Reference { get; set; } = string.Empty;

    /// <summary>
    /// Transaction type (Sent/Received)
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Transaction status
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Status code for programmatic handling
    /// </summary>
    [JsonPropertyName("statusCode")]
    public TransactionStatus StatusCode { get; set; }

    /// <summary>
    /// Transaction amount
    /// </summary>
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Formatted amount with currency symbol
    /// </summary>
    [JsonPropertyName("formattedAmount")]
    public string FormattedAmount => $"{Amount:C2}";

    /// <summary>
    /// Transaction description or memo
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Counterparty information
    /// </summary>
    [JsonPropertyName("counterparty")]
    public CounterpartyInfo Counterparty { get; set; } = new();

    /// <summary>
    /// Transaction timestamp
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Formatted date for display
    /// </summary>
    [JsonPropertyName("formattedDate")]
    public string FormattedDate => Timestamp.ToString("MMM dd, yyyy");

    /// <summary>
    /// Formatted time for display
    /// </summary>
    [JsonPropertyName("formattedTime")]
    public string FormattedTime => Timestamp.ToString("hh:mm tt");

    /// <summary>
    /// Relative time (e.g., "2 hours ago")
    /// </summary>
    [JsonPropertyName("relativeTime")]
    public string RelativeTime => GetRelativeTime(Timestamp);

    /// <summary>
    /// Transaction category for grouping
    /// </summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = "Transfer";

    /// <summary>
    /// Additional metadata
    /// </summary>
    [JsonPropertyName("metadata")]
    public TransactionMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Helper method to get relative time
    /// </summary>
    private static string GetRelativeTime(DateTime timestamp)
    {
        var timeSpan = DateTime.UtcNow - timestamp;

        if (timeSpan.TotalSeconds < 60)
            return "Just now";
        if (timeSpan.TotalMinutes < 60)
            return $"{Math.Floor(timeSpan.TotalMinutes)} minute{(Math.Floor(timeSpan.TotalMinutes) != 1 ? "s" : "")} ago";
        if (timeSpan.TotalHours < 24)
            return $"{Math.Floor(timeSpan.TotalHours)} hour{(Math.Floor(timeSpan.TotalHours) != 1 ? "s" : "")} ago";
        if (timeSpan.TotalDays < 7)
            return $"{Math.Floor(timeSpan.TotalDays)} day{(Math.Floor(timeSpan.TotalDays) != 1 ? "s" : "")} ago";
        if (timeSpan.TotalDays < 30)
            return $"{Math.Floor(timeSpan.TotalDays / 7)} week{(Math.Floor(timeSpan.TotalDays / 7) != 1 ? "s" : "")} ago";

        return timestamp.ToString("MMM dd, yyyy");
    }
}

/// <summary>
/// Counterparty information for transaction
/// </summary>
public class CounterpartyInfo
{
    /// <summary>
    /// Counterparty user ID
    /// </summary>
    [JsonPropertyName("userId")]
    public int UserId { get; set; }

    /// <summary>
    /// Counterparty full name
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Counterparty email
    /// </summary>
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Counterparty initials for avatar
    /// </summary>
    [JsonPropertyName("initials")]
    public string Initials => GetInitials(Name);

    /// <summary>
    /// Helper method to get initials from name
    /// </summary>
    private static string GetInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "??";

        var parts = name.Trim().Split(' ');
        if (parts.Length == 1)
            return parts[0][..Math.Min(2, parts[0].Length)].ToUpper();

        return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
    }
}

/// <summary>
/// Additional metadata for transaction
/// </summary>
public class TransactionMetadata
{
    /// <summary>
    /// Idempotency key if provided
    /// </summary>
    [JsonPropertyName("idempotencyKey")]
    public string? IdempotencyKey { get; set; }

    /// <summary>
    /// Failure reason if transaction failed
    /// </summary>
    [JsonPropertyName("failureReason")]
    public string? FailureReason { get; set; }

    /// <summary>
    /// IP address of the requestor
    /// </summary>
    [JsonPropertyName("ipAddress")]
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent of the requestor
    /// </summary>
    [JsonPropertyName("userAgent")]
    public string? UserAgent { get; set; }

    /// <summary>
    /// Correlation ID for tracing
    /// </summary>
    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }
}