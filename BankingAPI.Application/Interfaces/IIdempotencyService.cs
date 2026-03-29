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
    public interface IIdempotencyService
    {
        Task<TransferResponse?> GetCachedResponseAsync(string idempotencyKey);
        Task CacheResponseAsync(string idempotencyKey, TransferResponse response);
    }
}
