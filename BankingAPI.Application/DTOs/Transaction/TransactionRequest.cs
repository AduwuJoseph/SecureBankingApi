using BankingAPI.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Application.DTOs.Transaction
{
    public class TransactionRequest
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public TransactionType? Type { get; set; }
    }
}
