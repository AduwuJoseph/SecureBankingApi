using BankingAPI.Infrastructure.Repositories;
using BankingAPI.Infrastructure.Repositories.IRepo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Infrastructure.UnitOfWork
{
    public interface IUnitOfWork : IDisposable
    {
        IUserRepository Users { get; }
        IAccountRepository Accounts { get; }
        ITransactionRepository Transactions { get; }
        IAccountLedgerRepository AccountLedgers { get; }
        Task<int> CompleteAsync();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}
