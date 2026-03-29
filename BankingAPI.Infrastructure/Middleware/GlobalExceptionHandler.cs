using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using BankingAPI.Domain.Exceptions;

namespace BankingAPI.Infrastructure.Middleware;

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
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred: {Message}, CorrelationId: {CorrelationId}", ex.Message, correlationId);
            await HandleExceptionAsync(context, ex, correlationId);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception, string correlationId)
    {
        var response = context.Response;
        response.ContentType = "application/json";

        var errorResponse = new
        {
            Success = false,
            CorrelationId = correlationId,
            Timestamp = DateTime.UtcNow,
            Message = string.Empty,
            Details = string.Empty
        };

        switch (exception)
        {
            case NotFoundException notFound:
                response.StatusCode = StatusCodes.Status404NotFound;
                errorResponse = errorResponse with { Message = notFound.Message };
                break;

            case BusinessRuleException businessRule:
                response.StatusCode = StatusCodes.Status400BadRequest;
                errorResponse = errorResponse with { Message = businessRule.Message };
                break;

            case ValidationException validation:
                response.StatusCode = StatusCodes.Status400BadRequest;
                errorResponse = errorResponse with { Message = validation.Message };
                break;

            case UnauthorizedException unauthorized:
                response.StatusCode = StatusCodes.Status401Unauthorized;
                errorResponse = errorResponse with { Message = unauthorized.Message };
                break;

            default:
                response.StatusCode = StatusCodes.Status500InternalServerError;
                errorResponse = errorResponse with
                {
                    Message = "An unexpected error occurred",
                    Details = exception.Message
                };
                break;
        }

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        await response.WriteAsync(JsonSerializer.Serialize(errorResponse, options));
    }
}