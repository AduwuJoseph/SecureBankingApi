using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Application.DTOs
{
    public class ApiResponse
    {
        public string Code { get; set; }
        public string Message { get; set; }
    }

    public class ApiResponse<T>
    {
        public string Code { get; set; }
        public string Message { get; set; }
        public T Data { get; set; }
    }

    public class PagedResponse<T>
    {
        public string Code { get; set; }
        public string Message { get; set; }
        public T Data { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; }
    }
}
