using BankingAPI.Domain.Entities;
using BankingAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BankingAPI.Infrastructure.Data
{
    public static class DatabaseSeeder
    {
        public static async Task SeedAsync(BankingDbContext context)
        {
            if (await context.Users.AnyAsync())
                return;

            // Seed admin user
            var adminUser = new User
            {
                FullName = "System Administrator",
                Email = "admin@banking.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                IsEmailVerified = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await context.Users.AddAsync(adminUser);

            // Create admin account
            var adminAccount = new Account
            {
                UserId = adminUser.Id,
                AccountNumber = await GenerateUniqueAccountNumberAsync(context),
                Balance = 1000000,
                Currency = "NGN",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await context.Accounts.AddAsync(adminAccount);

            // Seed test users
            for (int i = 1; i <= 5; i++)
            {
                var user = new User
                {
                    FullName = $"Test User {i}",
                    Email = $"user{i}@banking.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@123"),
                    IsEmailVerified = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                await context.Users.AddAsync(user);

                var account = new Account
                {
                    UserId = user.Id,
                    AccountNumber = await GenerateUniqueAccountNumberAsync(context),
                    Balance = 10000,
                    Currency = "USD",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                await context.Accounts.AddAsync(account);
            }

            await context.SaveChangesAsync();
        }

        private static readonly Random _random = new();

        public static async Task<string> GenerateUniqueAccountNumberAsync(BankingDbContext context)
        {
            string accountNumber;

            do
            {
                accountNumber = Generate10DigitNumber();
            }
            while (await context.Accounts.AnyAsync(a => a.AccountNumber == accountNumber));

            return accountNumber;
        }

        private static string Generate10DigitNumber()
        {
            // First digit should not be 0 (to ensure 10 digits)
            var firstDigit = _random.Next(1, 10);
            var remaining = _random.Next(0, 1_000_000_000); // 9 digits

            return firstDigit.ToString() + remaining.ToString("D9");
        }
    }
}