using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Application.DTOs.Transfer
{
    public class TransferResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? TransactionReference { get; set; }
        public decimal? NewBalance { get; set; }
        public bool IsIdempotentResponse { get; set; }
        public string? RowVersion { get; set; } = string.Empty;
    }
}
