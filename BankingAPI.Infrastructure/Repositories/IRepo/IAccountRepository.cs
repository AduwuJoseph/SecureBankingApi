using BankingAPI.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Infrastructure.Repositories.IRepo
{
    public interface IAccountRepository : IRepository<Account>
    {
        Task<Account> GetAccountWithUserAsync(Guid accountId);
        Task<Account> GetByAccountNumberAsync(string accountNumber);
        Task<bool> UpdateBalanceAsync(Guid accountId, decimal newBalance, byte[] rowVersion);
        Task<decimal> GetBalanceWithLockAsync(Guid accountId);
    }
}
