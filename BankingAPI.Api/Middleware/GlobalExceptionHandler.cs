using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace BankingAPI.API.Middleware
{
    public class GlobalExceptionHandler
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(RequestDelegate next, ILogger<GlobalExceptionHandler> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            _logger.LogError(exception, "An unhandled exception occurred");

            var response = context.Response;
            response.ContentType = "application/json";

            var errorResponse = new ErrorResponse
            {
                TraceId = context.TraceIdentifier,
                Timestamp = DateTime.UtcNow
            };

            switch (exception)
            {
                case ValidationException validationEx:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    errorResponse.Message = "Validation failed";
                    errorResponse.Errors = validationEx.Errors;
                    break;

                case UnauthorizedAccessException:
                    response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    errorResponse.Message = "Unauthorized access";
                    break;

                case NotFoundException notFoundEx:
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    errorResponse.Message = notFoundEx.Message;
                    break;

                case ConcurrencyException concurrencyEx:
                    response.StatusCode = (int)HttpStatusCode.Conflict;
                    errorResponse.Message = concurrencyEx.Message;
                    errorResponse.Retryable = true;
                    break;

                case InsufficientFundsException:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    errorResponse.Message = "Insufficient funds";
                    break;

                case InvalidOperationException:
                case ArgumentException:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    errorResponse.Message = exception.Message;
                    break;

                default:
                    response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    errorResponse.Message = "An unexpected error occurred";
                    break;
            }

            var json = JsonSerializer.Serialize(errorResponse);
            await response.WriteAsync(json);
        }
    }
}

public class ErrorResponse
{
    public string TraceId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Message { get; set; }
    public object Errors { get; set; }
    public bool Retryable { get; set; }
}