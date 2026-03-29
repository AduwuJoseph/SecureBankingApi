using BankingAPI.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Domain.Entities
{
    public class Account: BaseEntity
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string AccountNumber { get; set; }
        public decimal Balance { get; set; }
        public string Currency { get; set; }
        public bool IsActive { get; set; }
        [Timestamp]
        public byte[] RowVersion { get; set; } // For concurrency handling
        public DateTime LastUpdated { get; set; }

        // Navigation properties
        public virtual User? User { get; set; }
        public virtual ICollection<AccountLedger>? AccountLedgers { get; set; }
    }
}
