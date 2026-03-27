using BankingAPI.Domain.Enum;
using BankingAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Application.Services
{
    public class AccountLedgerService
    {
        public readonly BankingDbContext _context;
        private readonly ILogger<AccountLedgerService> _logger;

        public AccountLedgerService(BankingDbContext context, ILogger<AccountLedgerService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Transaction> GetByReferenceAsync(string reference)
        {
            return await _context.Transactions
                .Include(t => t.SenderAccount)
                .Include(t => t.RecipientAccount)
                .FirstOrDefaultAsync(t => t.TransactionReference == reference);
        }

        public async Task<Transaction> GetByIdempotentKeyAsync(string idempotentKey)
        {
            return await _context.Transactions
                .FirstOrDefaultAsync(t => t.IdempotentKey == idempotentKey);
        }

        public async Task<IEnumerable<Transaction>> GetAccountTransactionsAsync(
            Guid accountId, int page, int pageSize)
        {
            return await _context.Transactions
                .Where(t => t.SenderAccountId == accountId || t.RecipientAccountId == accountId)
                .OrderByDescending(t => t.InitiatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(t => t.SenderAccount)
                .Include(t => t.RecipientAccount)
                .ToListAsync();
        }

        public async Task<IEnumerable<Transaction>> GetUserTransactionsAsync(
            Guid userId, int page, int pageSize)
        {
            // Get user's account first
            var user = await _context.Users
                .Include(u => u.Account)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user?.Account == null)
                return Enumerable.Empty<Transaction>();

            return await GetAccountTransactionsAsync(user.Account.Id, page, pageSize);
        }

        public async Task<IEnumerable<Transaction>> GetPendingTransactionsAsync()
        {
            return await _context.Transactions
                .Where(t => t.Status == TransactionStatus.Pending
                            && t.InitiatedAt <= DateTime.UtcNow.AddMinutes(-5))
                .ToListAsync();
        }

        public async Task<IEnumerable<Transaction>> GetFailedTransactionsAsync(DateTime since)
        {
            return await _context.Transactions
                .Where(t => t.Status == TransactionStatus.Failed
                            && t.InitiatedAt >= since)
                .ToListAsync();
        }

        public async Task<decimal> GetDailyTransferTotalAsync(Guid accountId, DateTime date)
        {
            var startDate = date.Date;
            var endDate = startDate.AddDays(1);

            return await _context.Transactions
                .Where(t => t.SenderAccountId == accountId
                            && t.Status == TransactionStatus.Completed
                            && t.Type == TransactionType.Transfer
                            && t.CompletedAt >= startDate
                            && t.CompletedAt < endDate)
                .SumAsync(t => t.Amount);
        }

        public async Task<Dictionary<DateTime, decimal>> GetWeeklyTransactionSummaryAsync(
            Guid accountId, DateTime endDate)
        {
            var startDate = endDate.AddDays(-7);

            var transactions = await _context.Transactions
                .Where(t => (t.SenderAccountId == accountId || t.RecipientAccountId == accountId)
                            && t.Status == TransactionStatus.Completed
                            && t.CompletedAt >= startDate
                            && t.CompletedAt <= endDate)
                .GroupBy(t => t.CompletedAt.Value.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(t => t.Amount) })
                .ToDictionaryAsync(g => g.Date, g => g.Total);

            return transactions;
        }
    }
}
