using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BankingAPI.Application.DTOs.Transaction
{
    public class TransferLimitsResponse
    {
        [JsonPropertyName("minimumAmount")]
        public decimal MinimumAmount { get; set; }

        [JsonPropertyName("maximumAmount")]
        public decimal MaximumAmount { get; set; }

        [JsonPropertyName("dailyLimit")]
        public decimal DailyLimit { get; set; }

        [JsonPropertyName("dailyUsed")]
        public decimal DailyUsed { get; set; }

        [JsonPropertyName("dailyRemaining")]
        public decimal DailyRemaining { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "USD";
    }
}
