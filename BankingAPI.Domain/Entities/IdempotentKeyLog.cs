using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Domain.Entities
{
    public class IdempotentKeyLog
    {
        [Key]
        public string Key { get; set; }

        public string TransactionReference { get; set; }

        public string Response { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
