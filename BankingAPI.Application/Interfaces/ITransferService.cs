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
        Task<ApiResponse<TransactionResponse>> TransferAsync(Guid userId, TransferRequest transferRequest);
        Task<ApiResponse<TransactionResponse>> GetTransactionStatusAsync(string transactionReference);
    }
}
