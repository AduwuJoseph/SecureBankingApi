using BankingAPI.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Application.Interfaces
{
    public interface IAccountLedgerService
    {
        Task<IEnumerable<AccountLedger>> GetAccountLedgerEntriesAsync(Guid accountId, DateTime startDate, DateTime endDate);
        Task<IEnumerable<AccountLedger>> GetTransactionLedgerEntriesAsync(Guid transactionId);
        Task<decimal> GetAccountBalanceAtDateAsync(Guid accountId, DateTime date);
        Task<bool> VerifyLedgerConsistencyAsync(Guid accountId);
        Task<IEnumerable<AccountLedger>> GetLedgersByDateRangeAsync(DateTime startDate, DateTime endDate);
    }
}
