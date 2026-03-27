using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Domain.Enum
{
    public enum TransactionType
    {
        Transfer = 1,
        Deposit = 2,
        Withdrawal = 3,
        Fee = 4
    }
}
