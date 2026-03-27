using BankingAPI.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Infrastructure.Repositories.IRepo
{
    public interface ITransactionRepository : IRepository<Transaction>
    {
        Task<Transaction> GetByReferenceAsync(string reference);
        Task<IEnumerable<Transaction>> GetAccountTransactionsAsync(Guid accountId, int page, int pageSize);
        Task<IEnumerable<Transaction>> GetUserTransactionsAsync(Guid userId, int page, int pageSize);
    }
}
