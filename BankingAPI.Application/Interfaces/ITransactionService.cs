using BankingAPI.Application.DTOs;
using BankingAPI.Application.DTOs.Transaction;
using BankingAPI.Application.DTOs.Transfer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Application.Interfaces
{
    public interface ITransactionService
    {
        /// <summary>
        /// Get transaction by ID with caching
        /// </summary>
        Task<TransactionViewModel?> GetTransactionByIdAsync(int userId, int transactionId);

        /// <summary>
        /// Get transaction history with pagination, filtering, and caching
        /// </summary>
        Task<TransactionHistoryResponse> GetTransactionHistoryAsync(
            int userId,
            TransactionHistoryRequest request);

        /// <summary>
        /// Get transaction summary with caching
        /// </summary>
        Task<AdminTransactionSummaryResponse> GetTransactionSummaryAsync(int userId, DateTime? startDate = null, DateTime? endDate = null);

        /// <summary>
        /// Get recent transactions with caching
        /// </summary>
        Task<IEnumerable<TransactionViewModel>> GetRecentTransactionsAsync(int userId, int count = 10);

        /// <summary>
        /// Invalidate transaction cache for a user
        /// </summary>
        Task InvalidateTransactionCacheAsync(int userId);

        /// <summary>
        /// Export transactions to CSV
        /// </summary>
        Task<byte[]> ExportTransactionsToCsvAsync(int userId, TransactionHistoryRequest request);
    }
}
