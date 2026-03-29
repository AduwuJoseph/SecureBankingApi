using BankingAPI.Application.DTOs.Transfer;
using BankingAPI.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Application.Interfaces
{
    public interface IAntiFraudService
    {
        Task<FraudCheckResult> CheckTransactionAsync(
            Account senderAccount,
            Account recipientAccount,
            decimal amount,
            CancellationToken cancellationToken = default);
    }
}
