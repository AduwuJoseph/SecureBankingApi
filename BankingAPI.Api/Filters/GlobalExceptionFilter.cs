using BankingAPI.Application.DTOs.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Net;

namespace BankingAPI.Api.Filters
{
    public class GlobalExceptionFilter : IExceptionFilter
    {
        private readonly ILogger<GlobalExceptionFilter> _logger;

        public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger)
        {
            _logger = logger;
        }

        public void OnException(ExceptionContext context)
        {
            var exception = context.Exception;

            // Log full exception details
            _logger.LogError(exception, "An unhandled exception occurred");

            var response = new ErrorResponse
            {
                Message = "An unexpected error occurred.",
                Detail = exception.Message, 
                RequestId = context.HttpContext.TraceIdentifier
            };

            context.Result = new ObjectResult(response)
            {
                StatusCode = GetStatusCode(exception)
            };

            context.ExceptionHandled = true;
        }

        private int GetStatusCode(Exception exception)
        {
            return exception switch
            {
                ArgumentException => (int)HttpStatusCode.BadRequest,
                UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                KeyNotFoundException => (int)HttpStatusCode.NotFound,
                _ => (int)HttpStatusCode.InternalServerError
            };
        }
    }
}
