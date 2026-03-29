using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Domain.Common
{
    public static class ApiResponseCodes
    {
        public static string Success => "200";
        public static string NotFound => "404";
        public static string ValidationError => "422";
        public static string InternalServerError => "500";

        public static string Unauthorized => "401";

        public static string Forbidden => "403";
    }
}
