using BankingAPI.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Domain.Entities
{
    public class Account: BaseEntity
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string AccountNumber { get; set; }
        public decimal Balance { get; set; }
        public string Currency { get; set; }
        public bool IsActive { get; set; }
        public byte[] RowVersion { get; set; } // For concurrency handling

        // Navigation properties
        public virtual User User { get; set; }
        public virtual ICollection<Transaction>? SentTransactions { get; set; }
        public virtual ICollection<Transaction>? ReceivedTransactions { get; set; }
        public virtual ICollection<AccountLedger>? AccountLedgers { get; set; }
    }
}
