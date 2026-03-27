using BankingAPI.Application.DTOs;
using BankingAPI.Application.DTOs.Account;
using BankingAPI.Application.Interfaces;
using BankingAPI.Domain.Entities;
using BankingAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Application.Services
{
    public class UserService : IUserService
    {
        private BankingDbContext _context;
        public UserService(BankingDbContext context)
        {
            _context = context;
        }

        public async Task<User> GetByEmailAsync(string email)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<User> GetUserWithAccountAsync(Guid userId)
        {
            return await _context.Users
                .Include(u => u.Account)
                .FirstOrDefaultAsync(u => u.Id == userId);
        }

        public async Task<bool> IsEmailUniqueAsync(string email, Guid? excludeUserId = null)
        {
            var query = _context.Users.Where(u => u.Email == email);

            if (excludeUserId.HasValue)
                query = query.Where(u => u.Id != excludeUserId.Value);

            return !await query.AnyAsync();
        }
    }
}
