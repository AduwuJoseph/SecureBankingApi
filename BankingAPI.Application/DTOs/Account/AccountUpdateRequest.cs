using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Application.DTOs.Account
{
    public class AccountUpdateRequest
    {
        [Required(ErrorMessage = "Phone number is required")]
        public string PhoneNumber { get; set; }

        [StringLength(200)]
        public string Address { get; set; }

        public string City { get; set; }

        public string Country { get; set; }

        public string PostalCode { get; set; }
    }
}
