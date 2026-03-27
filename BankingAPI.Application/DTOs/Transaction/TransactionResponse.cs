using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Application.DTOs.Transaction
{
    public class TransactionResponse
    {
        public Guid Id { get; set; }
        public string TransactionReference { get; set; }
        public string RecipientAccountNumber { get; set; }
        public string SenderAccountNumber { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public DateTime InitiatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
