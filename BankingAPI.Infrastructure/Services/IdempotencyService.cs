using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using BankingAPI.Application.DTOs.Transfer;
using BankingAPI.Application.Interfaces;
using BankingAPI.Domain.Entities;

namespace BankingAPI.Infrastructure.Services;

public class IdempotencyService : IIdempotencyService
{
    private readonly IBankingDbContext _context;
    private readonly ILogger<IdempotencyService> _logger;

    public IdempotencyService(IBankingDbContext context, ILogger<IdempotencyService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<TransferResponse?> GetCachedResponseAsync(string idempotencyKey)
    {
        var log = await _context.IdempotencyLogs
            .FirstOrDefaultAsync(l => l.IdempotencyKey == idempotencyKey);

        if (log == null || log.ExpiresAt < DateTime.UtcNow)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TransferResponse>(log.ResponseJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize cached response for key {IdempotencyKey}", idempotencyKey);
            return null;
        }
    }

    public async Task CacheResponseAsync(string idempotencyKey, TransferResponse response)
    {
        var log = new IdempotencyLog
        {
            IdempotencyKey = idempotencyKey,
            Endpoint = "transfer",
            RequestHash = ComputeHash(idempotencyKey),
            ResponseJson = JsonSerializer.Serialize(response),
            StatusCode = 200,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };

        _context.IdempotencyLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    private string ComputeHash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}