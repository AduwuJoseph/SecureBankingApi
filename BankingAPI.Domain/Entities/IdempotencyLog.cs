using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Domain.Entities
{
    public class IdempotencyLog
    {
        public int Id { get; set; }
        public string IdempotencyKey { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public string RequestHash { get; set; } = string.Empty;
        public string ResponseJson { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
    }
}
