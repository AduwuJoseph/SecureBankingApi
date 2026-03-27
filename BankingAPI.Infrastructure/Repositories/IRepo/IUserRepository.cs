using BankingAPI.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Infrastructure.Repositories.IRepo
{
    public interface IUserRepository : IRepository<User>
    {
        Task<User> GetByEmailAsync(string email);
        Task<User> GetUserWithAccountAsync(Guid userId);
        Task<bool> IsEmailUniqueAsync(string email, Guid? excludeUserId = null);
    }
}
