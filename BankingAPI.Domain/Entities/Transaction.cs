using BankingAPI.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Domain.Entities
{
    public class Transaction
    {

        public Guid Id { get; set; }
        public string TransactionReference { get; set; }
        public string IdempotentKey { get; set; }
        public Guid SenderAccountId { get; set; }
        public Guid RecipientAccountId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public string Description { get; set; }
        public TransactionStatus Status { get; set; }
        public TransactionType Type { get; set; }
        public decimal Fee { get; set; }
        public DateTime InitiatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string FailureReason { get; set; }

        // Navigation properties
        public virtual Account SenderAccount { get; set; }
        public virtual Account RecipientAccount { get; set; }
    }
}
