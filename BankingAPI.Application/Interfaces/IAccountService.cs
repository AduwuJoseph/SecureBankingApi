using BankingAPI.Application.DTOs;
using BankingAPI.Application.DTOs.Account;
using BankingAPI.Application.DTOs.Transaction;
using BankingAPI.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Application.Interfaces
{
    public interface IAccountService
    {
        Task<Account> GetAccountWithUserAsync(Guid accountId);
        Task<Account> GetByAccountNumberAsync(string accountNumber);
        Task<bool> UpdateBalanceAsync(Guid accountId, decimal newBalance, byte[] rowVersion);
        Task<decimal> GetBalanceWithLockAsync(Guid accountId);
        Task<decimal> GetDailyTransactionTotalAsync(Guid accountId, DateTime date);
        Task<bool> HasSufficientFundsAsync(Guid accountId, decimal amount);
        Task<IEnumerable<Transaction>> GetRecentTransactionsAsync(Guid accountId, int count);
        Task<ApiResponse<AccountResponse>> GetAccountDetailsAsync(Guid userId);
        Task<ApiResponse<decimal>> GetAccountBalanceAsync(Guid userId);
        Task<ApiResponse<IEnumerable<TransactionResponse>>> GetTransactionHistoryAsync(Guid userId, int page, int pageSize);
        Task<ApiResponse<bool>> UpdateAccountInfoAsync(Guid userId, string phoneNumber);
        Task<ApiResponse<Account>> GetAccountWithUserAsync(Guid accountId);
        Task<Account> GetByAccountNumberAsync(string accountNumber);
        Task<bool> UpdateBalanceAsync(Guid accountId, decimal newBalance, byte[] rowVersion);
        Task<decimal> GetBalanceWithLockAsync(Guid accountId);
    }
}
