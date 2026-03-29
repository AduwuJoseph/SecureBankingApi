using BankingAPI.Domain.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Domain.Entities
{
    public class AccountLedger
    {
        [Key]
        public long Id { get; set; }
        public int UserId { get; set; }
        public int AccountId { get; set; }
        public long TransactionId  { get; set; }
        public decimal PreviousBalance { get; set; }
        public decimal NewBalance { get; set; }
        public decimal Amount { get; set; }
        public LedgerEntryType EntryType { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation properties
        public virtual User? User { get; set; }
        public virtual Account? Account { get; set; }
        public virtual Transaction? Transaction { get; set; }
    }
}
