using BankingAPI.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BankingAPI.Application.DTOs.Transaction
{
    public class TransactionHistoryRequest
    {
        /// <summary>
        /// Page number (starts at 1)
        /// </summary>
        [JsonPropertyName("page")]
        public int Page { get; set; } = 1;

        /// <summary>
        /// Number of items per page
        /// </summary>
        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; } = 50;

        /// <summary>
        /// Filter by transaction type (Sent, Received, or null for all)
        /// </summary>
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        /// <summary>
        /// Filter by transaction status
        /// </summary>
        [JsonPropertyName("status")]
        public TransactionStatus? Status { get; set; }

        /// <summary>
        /// Filter by start date
        /// </summary>
        [JsonPropertyName("startDate")]
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// Filter by end date
        /// </summary>
        [JsonPropertyName("endDate")]
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Minimum amount filter
        /// </summary>
        [JsonPropertyName("minAmount")]
        public decimal? MinAmount { get; set; }

        /// <summary>
        /// Maximum amount filter
        /// </summary>
        [JsonPropertyName("maxAmount")]
        public decimal? MaxAmount { get; set; }

        /// <summary>
        /// Search term for counterparty name or email
        /// </summary>
        [JsonPropertyName("searchTerm")]
        public string? SearchTerm { get; set; }

        /// <summary>
        /// Sort by field (timestamp, amount)
        /// </summary>
        [JsonPropertyName("sortBy")]
        public string SortBy { get; set; } = "timestamp";

        /// <summary>
        /// Sort order (asc, desc)
        /// </summary>
        [JsonPropertyName("sortOrder")]
        public string SortOrder { get; set; } = "desc";
    }
}
