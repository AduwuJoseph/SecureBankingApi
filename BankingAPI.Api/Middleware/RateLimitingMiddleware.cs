using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;

namespace BankingAPI.API.Middleware
{
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMemoryCache _cache;
        private readonly ILogger<RateLimitingMiddleware> _logger;

        public RateLimitingMiddleware(
            RequestDelegate next,
            IMemoryCache cache,
            ILogger<RateLimitingMiddleware> logger)
        {
            _next = next;
            _cache = cache;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var endpoint = context.GetEndpoint();
            var rateLimitAttribute = endpoint?.Metadata.GetMetadata<RateLimitAttribute>();

            if (rateLimitAttribute != null)
            {
                var clientId = GetClientIdentifier(context);
                var cacheKey = $"rate_limit_{clientId}_{context.Request.Path}";

                var requestCount = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow =
                        TimeSpan.FromSeconds(rateLimitAttribute.PeriodInSeconds);
                    return new RateLimitInfo { Count = 0, ResetAt = DateTime.UtcNow.AddSeconds(rateLimitAttribute.PeriodInSeconds) };
                });

                if (requestCount.Count >= rateLimitAttribute.Limit)
                {
                    _logger.LogWarning("Rate limit exceeded for {ClientId} on {Path}",
                        clientId, context.Request.Path);

                    context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                    context.Response.Headers["Retry-After"] =
                        (requestCount.ResetAt - DateTime.UtcNow).TotalSeconds.ToString();

                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "Rate limit exceeded",
                        retryAfter = (requestCount.ResetAt - DateTime.UtcNow).TotalSeconds
                    });
                    return;
                }

                requestCount.Count++;
                _cache.Set(cacheKey, requestCount,
                    TimeSpan.FromSeconds(rateLimitAttribute.PeriodInSeconds));
            }

            await _next(context);
        }

        private string GetClientIdentifier(HttpContext context)
        {
            // Try to get user ID if authenticated
            var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
                return $"user_{userId}";

            // Otherwise use IP address
            var ipAddress = context.Connection.RemoteIpAddress?.ToString();
            return $"ip_{ipAddress}";
        }
    }

    public class RateLimitAttribute : Attribute
    {
        public int PeriodInSeconds { get; set; }
        public int Limit { get; set; }
    }

    public class RateLimitInfo
    {
        public int Count { get; set; }
        public DateTime ResetAt { get; set; }
    }
}