using BankingAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Infrastructure.Persistence
{
    public class BankingDbContext : DbContext
    {
        public BankingDbContext(DbContextOptions<BankingDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Account> Accounts { get; set; }
        public DbSet<IdempotentKeyLog> IdempotentKeyLogs { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<AccountLedger> AccountLedgers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
                entity.Property(e => e.FullName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.PasswordHash).IsRequired();
                entity.HasOne(e => e.Account)
                    .WithOne(e => e.User)
                    .HasForeignKey<Account>(e => e.UserId);
            });

            // Account configuration
            modelBuilder.Entity<Account>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.AccountNumber).IsUnique();
                entity.Property(e => e.AccountNumber).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Balance).HasPrecision(18, 2);
                entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
                entity.Property(e => e.RowVersion).IsRowVersion();
            });

            // Transaction configuration
            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TransactionReference).IsUnique();
                entity.HasIndex(e => e.IdempotentKey).IsUnique();
                entity.Property(e => e.TransactionReference).IsRequired().HasMaxLength(20);
                entity.Property(e => e.IdempotentKey).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Amount).HasPrecision(18, 2);
                entity.Property(e => e.Fee).HasPrecision(18, 2);
                entity.HasOne(e => e.SenderAccount)
                    .WithMany(e => e.SentTransactions)
                    .HasForeignKey(e => e.SenderAccountId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.RecipientAccount)
                    .WithMany(e => e.ReceivedTransactions)
                    .HasForeignKey(e => e.RecipientAccountId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // AccountLedger configuration
            modelBuilder.Entity<AccountLedger>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.PreviousBalance).HasPrecision(18, 2);
                entity.Property(e => e.NewBalance).HasPrecision(18, 2);
                entity.Property(e => e.Amount).HasPrecision(18, 2);
                entity.HasOne(e => e.Account)
                    .WithMany(e => e.AccountLedgers)
                    .HasForeignKey(e => e.AccountId);
                entity.HasOne(e => e.Transaction)
                    .WithMany()
                    .HasForeignKey(e => e.TransactionId);
            });

            // IdempotentKeyLog configuration
            modelBuilder.Entity<IdempotentKeyLog>(entity =>
            {
                entity.HasKey(e => e.Key);
                entity.HasIndex(e => e.TransactionReference).IsUnique();
            });
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Add audit information
            var entries = ChangeTracker.Entries();
            foreach (var entry in entries)
            {
                if (entry.Entity is User user)
                {
                    if (entry.State == EntityState.Added)
                        user.CreatedAt = DateTime.UtcNow;
                    else if (entry.State == EntityState.Modified)
                        user.UpdatedAt = DateTime.UtcNow;
                }
                else if (entry.Entity is Account account)
                {
                    if (entry.State == EntityState.Added)
                        account.CreatedAt = DateTime.UtcNow;
                    else if (entry.State == EntityState.Modified)
                        account.UpdatedAt = DateTime.UtcNow;
                }
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
