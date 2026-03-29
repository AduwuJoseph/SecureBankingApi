using BankingAPI.Application.DTOs.Transaction;
using BankingAPI.Domain.Entities;
using BankingAPI.Domain.Enum;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Application.Interfaces
{
    public interface IAccountLedgerService
    {
        Task<TransactionResponse?> GetByReferenceAsync(string reference);

        Task<IEnumerable<TransactionResponse>> GetAccountTransactionsAsync(int accountId, int page, int pageSize);

        Task<IEnumerable<TransactionResponse>> GetUserTransactionsAsync(int userId, int page, int pageSize);
        Task<IEnumerable<TransactionResponse>> GetPendingTransactionsAsync();

        Task<IEnumerable<Transaction>> GetFailedTransactionsAsync(DateTime since);
        Task<decimal> GetDailyTransferTotalAsync(int accountId, DateTime date);

        Task<Dictionary<DateTime, decimal>> GetWeeklyTransactionSummaryAsync(
            int accountId, DateTime endDate);
    }
}
