using BankingAPI.Application.DTOs;
using BankingAPI.Application.DTOs.Account;
using BankingAPI.Application.Interfaces;
using BankingAPI.Domain.Entities;
using BankingAPI.Infrastructure.Persistence;
using BankingAPI.Infrastructure.Repositories.IRepo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Application.Services
{
    public class AccountService : IAccountService
    {
        private readonly BankingDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AccountService> _logger;

        public AccountService(
            BankingDbContext context,
            IMemoryCache cache,
            ILogger<AccountService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        public async Task<Account> GetAccountWithUserAsync(Guid accountId)
        {
            return await _context.Accounts
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == accountId);
        }

        public async Task<Account> GetByAccountNumberAsync(string accountNumber)
        {
            return await _context.Accounts
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.AccountNumber == accountNumber);
        }

        public async Task<bool> UpdateBalanceAsync(Guid accountId, decimal newBalance, byte[] rowVersion)
        {
            try
            {
                // Use raw SQL for optimistic concurrency control
                var rowsAffected = await _context.Database.ExecuteSqlRawAsync(
                    @"UPDATE Accounts 
                      SET Balance = {0}, UpdatedAt = GETUTCDATE()
                      WHERE Id = {1} AND RowVersion = {2}",
                    newBalance, accountId, rowVersion);

                if (rowsAffected == 0)
                {
                    _logger.LogWarning("Concurrency conflict when updating balance for account {AccountId}",
                        accountId);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating balance for account {AccountId}", accountId);
                throw;
            }
        }

        public async Task<decimal> GetBalanceWithLockAsync(Guid accountId)
        {
            // Use pessimistic locking for critical operations
            var account = await _context.Accounts
                .FromSqlRaw("SELECT * FROM Accounts WITH (UPDLOCK, ROWLOCK) WHERE Id = {0}", accountId)
                .FirstOrDefaultAsync();

            return account?.Balance ?? 0;
        }

        public async Task<decimal> GetDailyTransactionTotalAsync(Guid accountId, DateTime date)
        {
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1);

            var total = await _context.Transactions
                .Where(t => (t.SenderAccountId == accountId || t.RecipientAccountId == accountId)
                            && t.Status == TransactionStatus.Completed
                            && t.CompletedAt >= startOfDay
                            && t.CompletedAt < endOfDay)
                .SumAsync(t => t.Amount);

            return total;
        }

        public async Task<bool> HasSufficientFundsAsync(Guid accountId, decimal amount)
        {
            var account = await GetByIdAsync(accountId);
            return account != null && account.Balance >= amount;
        }

        public async Task<IEnumerable<Transaction>> GetRecentTransactionsAsync(Guid accountId, int count)
        {
            return await _context.Transactions
                .Where(t => t.SenderAccountId == accountId || t.RecipientAccountId == accountId)
                .OrderByDescending(t => t.InitiatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<ApiResponse<AccountResponse>> GetAccountDetailsAsync(Guid userId)
        {
            var user = await _context.Users.GetUserWithAccountAsync(userId);
            if (user == null || user.Account == null)
                throw new NotFoundException("Account not found");

            return new AccountDto
            {
                Id = user.Account.Id,
                AccountNumber = user.Account.AccountNumber,
                Balance = user.Account.Balance,
                Currency = user.Account.Currency
            };
        }

        public async Task<decimal> GetAccountBalanceAsync(Guid userId)
        {
            var cacheKey = $"account_balance_{userId}";

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);

                var user = await _context.Users.GetUserWithAccountAsync(userId);
                if (user?.Account == null)
                    throw new NotFoundException("Account not found");

                return user.Account.Balance;
            });
        }

        public async Task<bool> UpdateAccountInfoAsync(Guid userId, string phoneNumber)
        {
            var user = await _context.Users.GetByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

            user.PhoneNumber = phoneNumber;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.Users.UpdateAsync(user);
            await _context.CompleteAsync();

            return true;
        } 

        // ROW-LEVEL LOCKING (Prevents race conditions)
        public async Task<Account> LockByIdAsync(Guid id)
        {
            return await _context.Accounts
                .FromSqlRaw("SELECT * FROM Accounts WHERE Id = {0} FOR UPDATE", id)
                .FirstAsync();
        }

    }
}
