using BankingAPI.Application.DTOs.Auth;
using BankingAPI.Application.Interfaces;
using BankingAPI.Application.Services;
using BankingAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankingAPI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly CurrentUserService _currentUserService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthController(
        IAuthService authService,
        CurrentUserService currentUserService,
        IHttpContextAccessor httpContextAccessor)
    {
        _authService = authService;
        _currentUserService = currentUserService;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest registerRequest)
    {
        var deviceInfo = GetDeviceInfo();
        var ipAddress = GetClientIpAddress();

        var result = await _authService.RegisterAsync(registerRequest, deviceInfo, ipAddress);

        // Set refresh token as HTTP-only cookie
        SetRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiry);

        return Ok(result);
    }

    /// <summary>
    /// Login user
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest loginRequest)
    {
        var deviceInfo = GetDeviceInfo();
        var ipAddress = GetClientIpAddress();

        var result = await _authService.LoginAsync(loginRequest, deviceInfo, ipAddress);

        // Set refresh token as HTTP-only cookie
        SetRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiry);

        return Ok(result);
    }

    /// <summary>
    /// Refresh access token using refresh token
    /// </summary>
    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest? refreshTokenRequest = null)
    {
        // Try to get refresh token from body first, then from cookie
        var refreshToken = refreshTokenRequest?.RefreshToken ?? Request.Cookies["refreshToken"];

        if (string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized(new { message = "Refresh token is required" });
        }

        var deviceInfo = GetDeviceInfo();
        var ipAddress = GetClientIpAddress();

        var result = await _authService.RefreshTokenAsync(
            new RefreshTokenRequest { RefreshToken = refreshToken },
            deviceInfo,
            ipAddress);

        // Set new refresh token as HTTP-only cookie
        SetRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiry);

        return Ok(result);
    }

    /// <summary>
    /// Revoke a specific refresh token
    /// </summary>
    [Authorize]
    [HttpPost("revoke-token")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RevokeToken([FromBody] RevokeTokenRequest revokeTokenRequest)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
        var result = await _authService.RevokeTokenAsync(revokeTokenRequest, userId);

        if (!result)
        {
            return BadRequest(new { message = "Failed to revoke token" });
        }

        return Ok(new { message = "Token revoked successfully" });
    }

    /// <summary>
    /// Logout user
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? logoutRequesr = null)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedAccessException();

        var logoutRequest = logoutRequesr ?? new LogoutRequest();

        // If no specific token provided, try to get from cookie
        if (string.IsNullOrEmpty(logoutRequest.RefreshToken))
        {
            logoutRequest.RefreshToken = Request.Cookies["refreshToken"];
        }

        await _authService.LogoutAsync(logoutRequest, userId);

        // Clear the refresh token cookie
        Response.Cookies.Delete("refreshToken");

        return Ok(new { message = "Logged out successfully" });
    }

    /// <summary>
    /// Get all refresh tokens for the current user
    /// </summary>
    [Authorize]
    [HttpGet("tokens")]
    [ProducesResponseType(typeof(IEnumerable<RefreshTokenInfoResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyTokens()
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
        var tokens = await _authService.GetUserRefreshTokensAsync(userId);
        return Ok(tokens);
    }

    /// <summary>
    /// Revoke all tokens for the current user
    /// </summary>
    [Authorize]
    [HttpPost("revoke-all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeAllTokens([FromBody] string? reason = null)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
        await _authService.RevokeAllUserTokensAsync(userId, reason);

        // Clear the refresh token cookie
        Response.Cookies.Delete("refreshToken");

        return Ok(new { message = "All tokens revoked successfully" });
    }

    private void SetRefreshTokenCookie(string? refreshToken, DateTime? expiry)
    {
        if (string.IsNullOrEmpty(refreshToken) || !expiry.HasValue)
            return;

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = expiry.Value,
            Path = "/api/auth"
        };

        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
    }

    private string GetDeviceInfo()
    {
        var userAgent = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();
        return string.IsNullOrEmpty(userAgent) ? "Unknown Device" : userAgent[..Math.Min(100, userAgent.Length)];
    }

    private string GetClientIpAddress()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null) return "Unknown";

        var ipAddress = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (string.IsNullOrEmpty(ipAddress))
        {
            ipAddress = context.Connection.RemoteIpAddress?.ToString();
        }

        return ipAddress ?? "Unknown";
    }
}