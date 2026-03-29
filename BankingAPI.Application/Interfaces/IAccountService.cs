using BankingAPI.Application.DTOs;
using BankingAPI.Application.DTOs.Account;
using BankingAPI.Application.DTOs.Transaction;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Application.Interfaces
{
    public interface IAccountService
    {
        Task<AccountInfoResponse?> GetAccountInfoAsync(int userId);
        Task<AccountInfoResponse?> GetAccountWithAccountNumberAsync(string accountNumber);

        Task<AccountInfoResponse?> UpdateContactInfoAsync(int userId, AccountUpdateRequest updateDto);

        Task<TransactionHistoryResponse> GetTransactionHistoryAsync(int userId, TransactionHistoryRequest request);

        Task<TransactionViewModel?> GetTransactionByIdAsync(int userId, int transactionId);
    }
}
