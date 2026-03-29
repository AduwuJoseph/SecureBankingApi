using BankingAPI.Application.DTOs;
using BankingAPI.Application.DTOs.Transaction;
using BankingAPI.Application.DTOs.Transfer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Application.Interfaces
{
    public interface ITransferService
    {
        /// <summary>
        /// Transfer funds from one user to another
        /// </summary>
        Task<TransferResponse> TransferFundsAsync(int senderId, TransferRequest transferRequest, string idempotencyKey,
        CancellationToken cancellationToken = default);

        /// <summary>
        /// Get daily transfer total for a user
        /// </summary>
        Task<decimal> GetDailyTransferTotalAsync(
            int userId,
            CancellationToken cancellationToken = default);
    }
}
