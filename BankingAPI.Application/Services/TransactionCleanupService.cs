// BankingAPI.Infrastructure/Services/TransactionCleanupService.cs
using BankingAPI.Application.Interfaces;
using BankingAPI.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BankingAPI.Infrastructure.Services
{
    public class TransactionCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TransactionCleanupService> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromHours(24);

        public TransactionCleanupService(
            IServiceProvider serviceProvider,
            ILogger<TransactionCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupFailedTransactions();
                    await CleanupExpiredIdempotentKeys();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during transaction cleanup");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }

        private async Task CleanupFailedTransactions()
        {
            using var scope = _serviceProvider.CreateScope();
            var _transferService = scope.ServiceProvider.GetRequiredService<ITransferService>();

            var cutoffDate = DateTime.UtcNow.AddDays(-30);
            var failedTransactions = await _transferService.Transactions
                .FindAsync(t => t.Status == TransactionStatus.Failed &&
                               t.InitiatedAt < cutoffDate);

            foreach (var transaction in failedTransactions)
            {
                await _transferService.Transactions.DeleteAsync(transaction);
            }

            await _transferService.CompleteAsync();
            _logger.LogInformation("Cleaned up {Count} failed transactions",
                failedTransactions.Count());
        }

        private async Task CleanupExpiredIdempotentKeys()
        {
            // Implementation for cleaning up old idempotent keys
            // This would typically remove keys older than 7 days
        }
    }
}