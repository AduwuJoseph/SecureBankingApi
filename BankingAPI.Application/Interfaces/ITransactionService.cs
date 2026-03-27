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
        Task<Transaction> GetByReferenceAsync(string reference);
        Task<Transaction> GetByIdempotentKeyAsync(string idempotentKey);
        Task<IEnumerable<Transaction>> GetAccountTransactionsAsync(Guid accountId, int page, int pageSize);
        Task<IEnumerable<Transaction>> GetUserTransactionsAsync(Guid userId, int page, int pageSize);
        Task<IEnumerable<Transaction>> GetPendingTransactionsAsync();
        Task<IEnumerable<Transaction>> GetFailedTransactionsAsync(DateTime since);
        Task<decimal> GetDailyTransferTotalAsync(Guid accountId, DateTime date);
        Task<Dictionary<DateTime, decimal>> GetWeeklyTransactionSummaryAsync(Guid accountId, DateTime endDate);
        Task<PagedResponse<IEnumerable<TransactionResponse>>> GetTransactions(TransactionRequest transactionRequest);

        Task<ApiResponse<TransactionResponse>> GetByReferenceAsync(string reference);
        Task<PagedResponse<IEnumerable<TransactionResponse>>> GetAccountTransactionsAsync(Guid accountId, int page, int pageSize);
        Task<PagedResponse<IEnumerable<TransactionResponse>>> GetUserTransactionsAsync(Guid userId, int page, int pageSize);
    }
}
