namespace BankingAPI.Application.Interfaces;

public interface IAuditService
{
    Task LogAsync(string action, string details, string? userId = null);
}