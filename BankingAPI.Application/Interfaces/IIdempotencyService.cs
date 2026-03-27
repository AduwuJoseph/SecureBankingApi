using BankingAPI.Application.DTOs;
using BankingAPI.Application.DTOs.Transaction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Application.Interfaces
{
    public interface IIdempotencyService
    {
        Task<(bool isDuplicate, TransactionResponse response)> IsDuplicateRequestAsync(string idempotencyKey);
        Task PushResponse(TransactionResponse response, string idempotencyKey);
    }
}
