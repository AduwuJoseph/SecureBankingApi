using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Application.DTOs.Transfer
{
    public class TransferRequest
    {

        [Required]
        public string RecipientAccountNumber { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }

        [StringLength(200)]
        public string Description { get; set; }

        [Required]
        public string IdempotentKey { get; set; }
    }
}
