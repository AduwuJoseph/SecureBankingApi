using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Application.DTOs.Transaction
{
    /// <summary>
    /// Transaction summary Response for admin dashboard, includes aggregated data and insights about transactions
    /// </summary>
    public class AdminTransactionSummaryResponse
    {
        public decimal TotalSent { get; set; }
        public decimal TotalReceived { get; set; }
        public decimal NetFlow => TotalReceived - TotalSent;
        public int TotalTransactions { get; set; }
        public int SuccessfulTransactions { get; set; }
        public int FailedTransactions { get; set; }
        public decimal AverageTransactionAmount { get; set; }
        public decimal LargestTransactionAmount { get; set; }
        public DateTime? LastTransactionDate { get; set; }
        public Dictionary<string, decimal> MonthlyBreakdown { get; set; } = new();
        public Dictionary<string, int> TopCounterparties { get; set; } = new();
    }
}
