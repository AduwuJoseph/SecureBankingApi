using BankingAPI.Application.DTOs.Account;
using BankingAPI.Application.DTOs.Auth;
using BankingAPI.Application.Interfaces;
using BankingAPI.Domain.Entities;
using BankingAPI.Domain.Exceptions;
using BankingAPI.Infrastructure.Data;
using BCrypt.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace BankingAPI.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly IBankingDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPasswordHasher _passwordHasher;

    public AuthService(
        IBankingDbContext context,
        IConfiguration configuration,
        ILogger<AuthService> logger,
        IAuditService auditService,
        IHttpContextAccessor httpContextAccessor,
        IPasswordHasher passwordHasher)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
        _passwordHasher = passwordHasher;
    }

    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest registerRequest,
        string? deviceInfo = null,
        string? ipAddress = null)
    {
        // Check if user already exists
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == registerRequest.Email);

        if (existingUser != null)
        {
            throw new BusinessRuleException("User with this email already exists");
        }

        // Create new user
        var user = new User
        {
            FullName = registerRequest.FullName,
            Email = registerRequest.Email,
            PasswordHash = _passwordHasher.HashPassword(registerRequest.Password),
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);

        // Create account for user
        var account = new Account
        {
            UserId = user.Id,
            Balance = 0,
            AccountNumber = await GenerateUniqueAccountNumberAsync(_context),
            IsActive = true,
            LastUpdated = DateTime.UtcNow
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        // Generate tokens
        var (accessToken, refreshToken, refreshTokenExpiry) = await GenerateTokensAsync(user, deviceInfo, ipAddress);

        _logger.LogInformation("User {Email} registered successfully", user.Email);
        await _auditService.LogAsync("User Registration",
            $"User {user.Email} registered from {ipAddress ?? "unknown IP"} with device {deviceInfo ?? "unknown device"}",
            user.Id.ToString());

        return new AuthResponse
        {
            Message = "Registration successful",
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            User = new UserResponse
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                IsActive = true,
                AccountNumber = account.AccountNumber
            },
            ExpiresIn = 3600,
            TokenType = "Bearer",
            RefreshTokenExpiry = refreshTokenExpiry
        };
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest loginRequest,
        string? deviceInfo = null,
        string? ipAddress = null)
    {
        var user = await _context.Users.Include(u => u.Account)
            .FirstOrDefaultAsync(u => u.Email == loginRequest.Email);

        if (user == null || !_passwordHasher.VerifyPassword(loginRequest.Password, user.PasswordHash))
        {
            throw new UnauthorizedException("Invalid email or password");
        }

        // Generate tokens
        var (accessToken, refreshToken, refreshTokenExpiry) = await GenerateTokensAsync(user, deviceInfo, ipAddress);

        _logger.LogInformation("User {Email} logged in successfully from {IpAddress}", user.Email, ipAddress);
        await _auditService.LogAsync("User Login",
            $"User {user.Email} logged in from {ipAddress ?? "unknown IP"} with device {deviceInfo ?? "unknown device"}",
            user.Id.ToString());

        return new AuthResponse
        {
            Message = "Login successful",
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            User = new UserResponse
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                IsActive = user.Account?.IsActive ?? false,
                AccountNumber = user.Account?.AccountNumber
            },
            ExpiresIn = 3600,
            TokenType = "Bearer",
            RefreshTokenExpiry = refreshTokenExpiry
        };
    }

    public async Task<AuthResponse> RefreshTokenAsync(
        RefreshTokenRequest refreshTokenRequest,
        string? deviceInfo = null,
        string? ipAddress = null)
    {
        var storeRequestken = await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == refreshTokenRequest.RefreshToken);

        if (storeRequestken == null)
        {
            throw new UnauthorizedException("Invalid refresh token");
        }

        if (storeRequestken.IsRevoked)
        {
            throw new UnauthorizedException("Refresh token has been revoked");
        }

        if (storeRequestken.IsUsed)
        {
            throw new UnauthorizedException("Refresh token has already been used");
        }

        if (storeRequestken.ExpiryDate < DateTime.UtcNow)
        {
            throw new UnauthorizedException("Refresh token has expired");
        }

        // Mark the old token as used
        storeRequestken.IsUsed = true;
        storeRequestken.RevokedAt = DateTime.UtcNow;
        storeRequestken.RevokedReason = "Used for refresh";

        // Generate new tokens
        var (accessToken, refreshToken, refreshTokenExpiry) = await GenerateTokensAsync(
            storeRequestken.User!,
            deviceInfo,
            ipAddress);

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} refreshed tokens successfully", storeRequestken.UserId);
        await _auditService.LogAsync("Token Refresh",
            $"User {storeRequestken.User!.Email} refreshed tokens from {ipAddress ?? "unknown IP"}",
            storeRequestken.UserId.ToString());

        return new AuthResponse
        {
            Message = "Token refreshed successfully",
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            User = new UserResponse
            {
                UserId = storeRequestken.UserId,
                FullName = storeRequestken.User!.FullName,
                Email = storeRequestken.User.Email,
            },
            ExpiresIn = 3600,
            TokenType = "Bearer",
            RefreshTokenExpiry = refreshTokenExpiry
        };
    }

    public async Task<bool> RevokeTokenAsync(RevokeTokenRequest revokeTokenRequest, int userId)
    {
        var storeRequestken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == revokeTokenRequest.RefreshToken && rt.UserId == userId);

        if (storeRequestken == null)
        {
            return false;
        }

        storeRequestken.IsRevoked = true;
        storeRequestken.RevokedAt = DateTime.UtcNow;
        storeRequestken.RevokedReason = revokeTokenRequest.Reason ?? "Revoked by user";

        await _context.SaveChangesAsync();

        _logger.LogInformation("Refresh token revoked for user {UserId}", userId);
        await _auditService.LogAsync("Token Revoked",
            $"User {userId} revoked token. Reason: {revokeTokenRequest.Reason ?? "Not specified"}",
            userId.ToString());

        return true;
    }

    public async Task<bool> LogoutAsync(LogoutRequest logoutRequest, int userId)
    {
        if (logoutRequest.AllDevices)
        {
            // Revoke all tokens for the user
            var userTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == userId && !rt.IsRevoked)
                .ToListAsync();

            foreach (var token in userTokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
                token.RevokedReason = "Logout from all devices";
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} logged out from all devices", userId);
            await _auditService.LogAsync("Logout All",
                $"User {userId} logged out from all devices",
                userId.ToString());
        }
        else if (!string.IsNullOrEmpty(logoutRequest.RefreshToken))
        {
            // Revoke only the specific token
            var token = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == logoutRequest.RefreshToken && rt.UserId == userId);

            if (token != null)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
                token.RevokedReason = "Logout";

                await _context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} logged out from specific device", userId);
                await _auditService.LogAsync("Logout",
                    $"User {userId} logged out from specific device",
                    userId.ToString());
            }
        }

        return true;
    }

    public async Task<IEnumerable<RefreshTokenInfoResponse>> GetUserRefreshTokensAsync(int userId)
    {
        var tokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId)
            .OrderByDescending(rt => rt.CreatedAt)
            .ToListAsync();

        return tokens.Select(t => new RefreshTokenInfoResponse
        {
            Id = t.Id,
            Token = MaskToken(t.Token),
            ExpiryDate = t.ExpiryDate,
            IsRevoked = t.IsRevoked,
            IsUsed = t.IsUsed,
            DeviceInfo = t.DeviceInfo,
            IpAddress = t.IpAddress,
            CreatedAt = t.CreatedAt,
            RevokedAt = t.RevokedAt,
            RevokedReason = t.RevokedReason
        });
    }

    public async Task<bool> RevokeAllUserTokensAsync(int userId, string? reason = null)
    {
        var tokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedReason = reason ?? "Revoked all tokens by admin";
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("All tokens revoked for user {UserId}", userId);
        await _auditService.LogAsync("Revoke All Tokens",
            $"All tokens revoked for user {userId}. Reason: {reason ?? "Not specified"}",
            userId.ToString());

        return true;
    }
    private static readonly Random _random = new();

    public static async Task<string> GenerateUniqueAccountNumberAsync(IBankingDbContext context)
    {
        string accountNumber;

        do
        {
            accountNumber = Generate10DigitNumber();
        }
        while (await context.Accounts.AnyAsync(a => a.AccountNumber == accountNumber));

        return accountNumber;
    }

    private static string Generate10DigitNumber()
    {
        // First digit should not be 0 (to ensure 10 digits)
        var firstDigit = _random.Next(1, 10);
        var remaining = _random.Next(0, 1_000_000_000); // 9 digits

        return firstDigit.ToString() + remaining.ToString("D9");
    }
    private async Task<(string AccessToken, string RefreshToken, DateTime Expiry)> GenerateTokensAsync(
        User user,
        string? deviceInfo,
        string? ipAddress)
    {
        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();
        var jwtId = Guid.NewGuid().ToString();
        var refreshTokenExpiry = DateTime.UtcNow.AddDays(
            _configuration.GetValue<int>("JwtSettings:RefreshTokenExpirationDays", 7));

        // Store refresh token
        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            JwtId = jwtId,
            ExpiryDate = refreshTokenExpiry,
            DeviceInfo = deviceInfo ?? GetDefaultDeviceInfo(),
            IpAddress = ipAddress ?? GetClientIpAddress(),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false,
            IsUsed = false
        };

        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        return (accessToken, refreshToken, refreshTokenExpiry);
    }

    private string GenerateAccessToken(User user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var key = Encoding.ASCII.GetBytes(jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret not configured"));
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenId = Guid.NewGuid().ToString();

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.NameId, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Name, user.FullName),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, tokenId),
            new Claim(JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
            new Claim("userId", user.Id.ToString()),
            new Claim("fullName", user.FullName),
            new Claim("email", user.Email)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(double.Parse(jwtSettings["ExpirationMinutes"] ?? "60")),
            Issuer = jwtSettings["Issuer"],
            Audience = jwtSettings["Audience"],
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    private string MaskToken(string token)
    {
        if (string.IsNullOrEmpty(token) || token.Length < 12)
            return "***";

        return $"{token[..6]}...{token[^6..]}";
    }

    private string GetDefaultDeviceInfo()
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