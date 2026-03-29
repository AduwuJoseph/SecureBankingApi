using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace BankingAPI.Infrastructure.Services;

public class CurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? UserId
    {
        get
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }

            // Fallback to middleware set value
            if (_httpContextAccessor.HttpContext?.Items["UserId"] is int id)
            {
                return id;
            }

            return null;
        }
    }

    public string? CorrelationId
    {
        get
        {
            return _httpContextAccessor.HttpContext?.Items["CorrelationId"]?.ToString();
        }
    }
}