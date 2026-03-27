using BankingAPI.Application.DTOs;
using BankingAPI.Application.DTOs.Account;
using BankingAPI.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Application.Interfaces
{
    public interface IUserService
    {
        Task<ApiResponse<UserResponse>> GetByEmailAsync(string email);
        Task<ApiResponse<UserResponse>> GetUserWithAccountAsync(Guid userId);
        Task<bool> IsEmailUniqueAsync(string email, Guid? excludeUserId = null);
    }
}
