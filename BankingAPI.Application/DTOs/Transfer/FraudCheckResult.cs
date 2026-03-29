using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Application.DTOs.Transfer
{
    public class FraudCheckResult
    {
        public bool IsApproved { get; set; }
        public string? Reason { get; set; }
        public decimal? RiskScore { get; set; }
    }
}
