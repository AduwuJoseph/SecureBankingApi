using BankingAPI.Domain.Common;
using BankingAPI.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Domain.Entities
{
    public class Transaction: BaseEntity
    {
        public long Id { get; set; }
        public int SenderId { get; set; }
        public int RecipientId { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public string? TransactionReference { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
        public TransactionType TransactionType { get; set; }
        public string? FailureReason { get; set; }
        public string? IdempotencyKey { get; set; }

        // Navigation properties
        public virtual User? Sender { get; set; }
        public virtual User? Recipient { get; set; }
        public virtual ICollection<AccountLedger> LedgerEntries { get; set; } = new List<AccountLedger>();
    }
}
