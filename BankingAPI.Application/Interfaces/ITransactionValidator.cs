using BankingAPI.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Application.Interfaces
{
    public interface ITransactionValidator
    {
        Task ValidateTransferAsync(Account senderAccount, string recipientAccountNumber, decimal amount);
        Task ValidateWithdrawalAsync(Account account, decimal amount);
        Task ValidateDepositAsync(Account account, decimal amount);
        Task<bool> CheckDailyLimitAsync(Guid accountId, decimal amount);
        Task<bool> CheckMonthlyLimitAsync(Guid accountId, decimal amount);
        Task<bool> PerformAntiFraudCheckAsync(Account sender, Account recipient, decimal amount);
    }
}
