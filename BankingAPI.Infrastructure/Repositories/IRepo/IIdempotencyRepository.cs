using BankingAPI.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Infrastructure.Repositories.IRepo
{
    public interface IIdempotencyRepository : IRepository<IdempotentKeyLog>
    {
        Task<Transaction> GetByIdempotentKeyAsync(string idempotentKey);
    }
}
