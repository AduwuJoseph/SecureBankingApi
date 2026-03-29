using BankingAPI.Application.DTOs.Transfer;
using BankingAPI.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Application.Interfaces
{
    public interface ITransactionValidator
    {
        /// <summary>
        /// Validates a transfer transaction
        /// </summary>
        Task<ValidationResult> ValidateTransferAsync(
            Account senderAccount,
            string recipientAccountNumber,
            decimal amount,
            CancellationToken cancellationToken = default);
    }
}
