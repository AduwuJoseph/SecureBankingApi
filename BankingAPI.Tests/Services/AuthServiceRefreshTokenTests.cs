using BankingAPI.Application.DTOs.Auth;
using BankingAPI.Application.Interfaces;
using BankingAPI.Application.Services;
using BankingAPI.Domain.Entities;
using BankingAPI.Domain.Exceptions;
using BankingAPI.Infrastructure.Data;
using BankingAPI.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace BankingAPI.UnitTests.Services;

public class AuthServiceRefreshTokenTests
{
    private readonly IBankingDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    private readonly IAuditService _auditService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuthService _authService;

    public AuthServiceRefreshTokenTests()
    {
        _context = Substitute.For<IBankingDbContext>();
        _configuration = Substitute.For<IConfiguration>();
        _logger = Substitute.For<ILogger<AuthService>>();
        _auditService = Substitute.For<IAuditService>();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _authService = new AuthService(_context, _configuration, _logger, _auditService, _httpContextAccessor, _passwordHasher);

        SetupConfiguration();
        SeedDatabase();
    }

    private void SetupConfiguration()
    {
        var jwtSettings = new Dictionary<string, string>
        {
            ["Secret"] = "TestSecretKeyForJWTTokenGeneration1234567890!@#$%",
            ["Issuer"] = "TestIssuer",
            ["Audience"] = "TestAudience",
            ["ExpirationMinutes"] = "60",
            ["RefreshTokenExpirationDays"] = "7"
        };

        var configurationRoot = new ConfigurationBuilder()
            .AddInMemoryCollection(jwtSettings)
            .Build();

        _configuration.GetSection("JwtSettings").Returns(configurationRoot.GetSection("JwtSettings"));
    }

    private void SeedDatabase()
    {
        var user = new User
        {
            Id = 1,
            FullName = "Test User",
            Email = "test@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        _context.SaveChangesAsync();
    }

    [Fact]
    public async Task RegisterAsync_ShouldReturnTokens()
    {
        // Arrange
        var registerDto = new RegisterRequest
        {
            FullName = "New User",
            Email = "newuser@example.com",
            Password = "password123",
            ConfirmPassword = "password123"
        };

        // Act
        var result = await _authService.RegisterAsync(registerDto, "Test Device", "127.0.0.1");

        // Assert
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.TokenType.Should().Be("Bearer");
        result.ExpiresIn.Should().Be(3600);
        result.RefreshTokenExpiry.Should().NotBeNull();

        // Verify refresh token was stored
        var storedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == result.RefreshToken);

        storedToken.Should().NotBeNull();
        storedToken!.UserId.Should().Be(result.User.UserId);
        storedToken.DeviceInfo.Should().Be("Test Device");
        storedToken.IpAddress.Should().Be("127.0.0.1");
        storedToken.IsRevoked.Should().BeFalse();
        storedToken.IsUsed.Should().BeFalse();
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnTokens()
    {
        // Arrange
        var loginDto = new LoginRequest
        {
            Email = "test@example.com",
            Password = "password123"
        };

        // Act
        var result = await _authService.LoginAsync(loginDto, "Test Device", "127.0.0.1");

        // Assert
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();

        // Verify refresh token was stored
        var storedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == result.RefreshToken);

        storedToken.Should().NotBeNull();
        storedToken!.UserId.Should().Be(1);
    }

    [Fact]
    public async Task RefreshTokenAsync_ValidToken_ShouldReturnNewTokens()
    {
        // Arrange
        var loginDto = new LoginRequest
        {
            Email = "test@example.com",
            Password = "password123"
        };

        var loginResult = await _authService.LoginAsync(loginDto, "Test Device", "127.0.0.1");
        var oldRefreshToken = loginResult.RefreshToken;

        var refreshTokenDto = new RefreshTokenRequest
        {
            RefreshToken = oldRefreshToken!
        };

        // Act
        var result = await _authService.RefreshTokenAsync(refreshTokenDto, "New Device", "127.0.0.1");

        // Assert
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBe(oldRefreshToken);

        // Verify old token was marked as used
        var oldStoredToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == oldRefreshToken);

        oldStoredToken.Should().NotBeNull();       
    }
}