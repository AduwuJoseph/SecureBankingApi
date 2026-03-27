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
                Id = Guid.NewGuid(),
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
                Id = Guid.NewGuid(),
                UserId = adminUser.Id,
                AccountNumber = GenerateAccountNumber(),
                Balance = 1000000,
                Currency = "USD",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await context.Accounts.AddAsync(adminAccount);

            // Seed test users
            for (int i = 1; i <= 5; i++)
            {
                var user = new User
                {
                    Id = Guid.NewGuid(),
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
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    AccountNumber = GenerateAccountNumber(),
                    Balance = 10000,
                    Currency = "USD",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                await context.Accounts.AddAsync(account);
            }

            await context.SaveChangesAsync();
        }

        private static string GenerateAccountNumber()
        {
            var random = new Random();
            return $"ACC{DateTime.UtcNow:yyyyMMdd}{random.Next(100000, 999999)}";
        }
    }
}