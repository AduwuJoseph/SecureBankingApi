using BankingAPI.Application.Interfaces;
using BankingAPI.Domain.Entities;
using BankingAPI.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Infrastructure.Data
{
    public class BankingDbContext : DbContext, IBankingDbContext
    {
        public BankingDbContext(DbContextOptions<BankingDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Account> Accounts => Set<Account>();
        public DbSet<AccountLedger> AccountLedgers => Set<AccountLedger>();
        public DbSet<Transaction> Transactions => Set<Transaction>();
        public DbSet<IdempotencyLog> IdempotencyLogs => Set<IdempotencyLog>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new UserConfiguration());
            modelBuilder.ApplyConfiguration(new AccountConfiguration());
            modelBuilder.ApplyConfiguration(new AccountLedgerConfiguration());
            modelBuilder.ApplyConfiguration(new TransactionConfiguration());
            modelBuilder.ApplyConfiguration(new IdempotencyLogConfiguration());
            modelBuilder.ApplyConfiguration(new RefreshTokenConfiguration());

            base.OnModelCreating(modelBuilder);
        }

        public DatabaseFacade Database => base.Database;

        public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            return await Database.BeginTransactionAsync(cancellationToken);
        }

        public async Task ReloadEntityAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class
        {
            await Entry(entity).ReloadAsync(cancellationToken);
        }

        public async Task<byte[]> GetCurrentRowVersionAsync(int userId, CancellationToken cancellationToken = default)
        {
            var account = await Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);

            return account?.RowVersion ?? Array.Empty<byte>();
        }

        public IQueryable<T> FromSqlRaw<T>(string sql, params object[] parameters) where T : class
        {
            return Set<T>().FromSqlRaw(sql, parameters);
        }

        public void SetEntityState<T>(T entity, EntityState state) where T : class
        {
            Entry(entity).State = state;
        }

        public new Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<T> Entry<T>(T entity) where T : class
        {
            return base.Entry(entity);
        }
    }
}
