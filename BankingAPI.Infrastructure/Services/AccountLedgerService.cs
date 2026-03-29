using BankingAPI.Domain.Enum;
using BankingAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BankingAPI.Application.Interfaces;
using BankingAPI.Domain.Entities;
using BankingAPI.Application.DTOs.Transaction;

namespace BankingAPI.Infrastructure.Services
{
    public class AccountLedgerService: IAccountLedgerService
    {
        public readonly IBankingDbContext _context;
        private readonly ILogger<AccountLedgerService> _logger;

        public AccountLedgerService(IBankingDbContext context, ILogger<AccountLedgerService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<TransactionResponse?> GetByReferenceAsync(string reference)
        {
            return await _context.Transactions
                .Include(t => t.Sender)
                .Include(t => t.Recipient)
                .Where(t => t.TransactionReference == reference)
                .Select(x => new TransactionResponse
                {
                    RecipientEmail = x.Recipient.Email,
                    RecipientName = x.Recipient.FullName,
                    SenderEmail = x.Sender.Email,
                    SenderName = x.Sender.FullName,
                    Amount = x.Amount,
                    Description = x.Description,
                    TransactionReference = x.TransactionReference,
                    Timestamp = x.Timestamp,
                    Status = x.Status,
                    TransactionType = x.TransactionType.ToString(),
                    FailureReason = x.FailureReason
                })
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<TransactionResponse>> GetAccountTransactionsAsync(
            int accountId, int page, int pageSize)
        {
            return await _context.Transactions
                .Where(t => t.SenderId == accountId || t.RecipientId == accountId)
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(t => t.Sender)
                .Include(t => t.Recipient)
                .Select(t => new TransactionResponse
                {
                    RecipientEmail = t.Recipient.Email,
                    RecipientName = t.Recipient.FullName,
                    SenderEmail = t.Sender.Email,
                    SenderName = t.Sender.FullName,
                    Amount = t.Amount,
                    Description = t.Description,
                    TransactionReference = t.TransactionReference,
                    Timestamp = t.Timestamp,
                    Status = t.Status,
                    TransactionType = t.TransactionType.ToString(),
                    FailureReason = t.FailureReason
                })
                .ToListAsync();
        }

        public async Task<IEnumerable<TransactionResponse>> GetUserTransactionsAsync(
            int userId, int page, int pageSize)
        {
            // Get user's account first
            var user = await _context.Users
                .Include(u => u.Account)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user?.Account == null)
                return Enumerable.Empty<TransactionResponse>();

            return await GetAccountTransactionsAsync(user.Account.Id, page, pageSize);
        }

        public async Task<IEnumerable<TransactionResponse>> GetPendingTransactionsAsync()
        {
            return await _context.Transactions
                .Where(t => t.Status == TransactionStatus.Pending
                            && t.CreatedAt <= DateTime.UtcNow.AddMinutes(-5))
                .Select(t => new TransactionResponse
                {
                    RecipientEmail = t.Recipient.Email,
                    RecipientName = t.Recipient.FullName,
                    SenderEmail = t.Sender.Email,
                    SenderName = t.Sender.FullName,
                    Amount = t.Amount,
                    Description = t.Description,
                    TransactionReference = t.TransactionReference,
                    Timestamp = t.Timestamp,
                    Status = t.Status,
                    TransactionType = t.TransactionType.ToString(),
                    FailureReason = t.FailureReason
                })
                .ToListAsync();
        }

        public async Task<IEnumerable<Transaction>> GetFailedTransactionsAsync(DateTime since)
        {
            return await _context.Transactions
                .Where(t => t.Status == TransactionStatus.Failed
                            && t.CreatedAt >= since)
                .ToListAsync();
        }

        public async Task<decimal> GetDailyTransferTotalAsync(int accountId, DateTime date)
        {
            var startDate = date.Date;
            var endDate = startDate.AddDays(1);

            return await _context.Transactions
                .Where(t => t.SenderId == accountId
                            && t.Status == TransactionStatus.Completed
                            && t.TransactionType == TransactionType.Transfer
                            && t.CreatedAt >= startDate
                            && t.CreatedAt < endDate)
                .SumAsync(t => t.Amount);
        }

        public async Task<Dictionary<DateTime, decimal>> GetWeeklyTransactionSummaryAsync(
            int accountId, DateTime endDate)
        {
            var startDate = endDate.AddDays(-7);

            var transactions = await _context.Transactions
                .Where(t => (t.SenderId == accountId || t.RecipientId == accountId)
                            && t.Status == TransactionStatus.Completed
                            && t.CreatedAt >= startDate
                            && t.CreatedAt <= endDate)
                .GroupBy(t => t.CreatedAt.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(t => t.Amount) })
                .ToDictionaryAsync(g => g.Date, g => g.Total);

            return transactions;
        }
    }
}
