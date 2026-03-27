using BankingAPI.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace BankingAPI.Domain.Entities
{
    public class User: BaseEntity
    {
        public Guid Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string PhoneNumber { get; set; }
        public bool IsEmailVerified { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastLoginAt { get; set; }

        // Navigation properties
        public virtual Account Account { get; set; }
        public virtual ICollection<System.Transactions.Transaction> SentTransactions { get; set; }
        public virtual ICollection<System.Transactions.Transaction> ReceivedTransactions { get; set; }
    }
}
