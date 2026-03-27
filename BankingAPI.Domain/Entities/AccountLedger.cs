using BankingAPI.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Domain.Entities
{
    public class AccountLedger
    {
        public Guid Id { get; set; }
        public Guid AccountId { get; set; }
        public Guid TransactionId { get; set; }
        public decimal PreviousBalance { get; set; }
        public decimal NewBalance { get; set; }
        public decimal Amount { get; set; }
        public LedgerEntryType EntryType { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation properties
        public virtual Account Account { get; set; }
        public virtual Transaction Transaction { get; set; }
    }
}
