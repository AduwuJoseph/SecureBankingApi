using Microsoft.Extensions.Logging;
using BankingAPI.Application.Interfaces;
using Serilog.Context;

namespace BankingAPI.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly ILogger<AuditService> _logger;

    public AuditService(ILogger<AuditService> logger)
    {
        _logger = logger;
    }

    public Task LogAsync(string action, string details, string? userId = null)
    {
        using (LogContext.PushProperty("Action", action))
        using (LogContext.PushProperty("UserId", userId ?? "Anonymous"))
        using (LogContext.PushProperty("Details", details))
        {
            _logger.LogInformation("Audit: {Action} performed by UserId: {UserId} - {Details}",
                action, userId ?? "Anonymous", details);
        }

        return Task.CompletedTask;
    }
}