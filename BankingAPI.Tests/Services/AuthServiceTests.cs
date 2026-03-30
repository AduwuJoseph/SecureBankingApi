using BankingAPI.Application.DTOs.Auth;
using BankingAPI.Application.Interfaces;
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
using System;
using System.Threading.Tasks;
using Xunit;

namespace BankingAPI.UnitTests.Services;

public class AuthServiceTests
{
    private readonly BankingDbContext _context; // real InMemory DbContext
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    private readonly IAuditService _auditService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        // Use a unique in-memory database for isolation
        var options = new DbContextOptionsBuilder<BankingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new BankingDbContext(options);

        _configuration = Substitute.For<IConfiguration>();
        _logger = Substitute.For<ILogger<AuthService>>();
        _auditService = Substitute.For<IAuditService>();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();

        SetupConfiguration();
        SetupHttpContext();

        _sut = new AuthService(
            _context,
            _configuration,
            _logger,
            _auditService,
            _httpContextAccessor,
            _passwordHasher);
    }

    private void SetupConfiguration()
    {
        var settings = new Dictionary<string, string>
        {
            ["JwtSettings:Secret"] = "SuperSecretKeyForJwtToken1234567890",
            ["JwtSettings:Issuer"] = "TestIssuer",
            ["JwtSettings:Audience"] = "TestAudience",
            ["JwtSettings:ExpirationMinutes"] = "60",              // string is fine
            ["JwtSettings:RefreshTokenExpirationDays"] = "7"
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        _configuration.GetSection("JwtSettings").Returns(config.GetSection("JwtSettings"));

        _configuration.GetSection("JwtSettings").Returns(config.GetSection("JwtSettings"));
        _configuration["JwtSettings:Secret"].Returns("SuperSecretKeyForJwtToken1234567890");
        _configuration["JwtSettings:Issuer"].Returns("TestIssuer");
        _configuration["JwtSettings:Audience"].Returns("TestAudience");
        _configuration["JwtSettings:ExpirationMinutes"].Returns("60");
        _configuration["JwtSettings:RefreshTokenExpirationDays"].Returns("7");
    }

    private void SetupHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["User-Agent"] = "UnitTest-Agent";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");
        _httpContextAccessor.HttpContext.Returns(context);
    }

    // ========================= TESTS =========================

    [Fact]
    public async Task RegisterAsync_Should_Create_User_And_Account()
    {
        var request = new RegisterRequest
        {
            FullName = "John Doe",
            Email = "john@test.com",
            Password = "password",
            ConfirmPassword = "password"
        };

        _passwordHasher.HashPassword("password").Returns("hashed");

        var result = await _sut.RegisterAsync(request);

        result.Message.Should().Be("Registration successful");
        result.User.Email.Should().Be("john@test.com");

        (await _context.Users.CountAsync()).Should().Be(1);
        (await _context.Accounts.CountAsync()).Should().Be(1);

        await _auditService.Received(1).LogAsync(
            "User Registration",
            Arg.Any<string>(),
            Arg.Any<string>());
    }

    [Fact]
    public async Task RegisterAsync_Should_Throw_When_Email_Exists()
    {
        var existingUser = new User
        {
            Email = "john@test.com",
            FullName = "Existing",
            PasswordHash = "hashed"
        };
        await _context.Users.AddAsync(existingUser);
        await _context.SaveChangesAsync();

        var request = new RegisterRequest
        {
            FullName = "John",
            Email = "john@test.com",
            Password = "pass",
            ConfirmPassword = "pass"
        };

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.RegisterAsync(request));

        ex.Message.Should().Be("User with this email already exists");
    }

    [Fact]
    public async Task LoginAsync_Should_Succeed_With_Valid_Credentials()
    {
        var user = new User
        {
            Email = "john@test.com",
            PasswordHash = "hashed",
            FullName = "John",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            IsEmailVerified = true,
            LastLoginAt = DateTime.UtcNow
        };

        var account = new Account
        {
            AccountNumber = "1234567890",
            IsActive = true,
            User = user,
            Currency = "NGN",
            LastUpdated = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Balance = 0.0m,
            RowVersion = BitConverter.GetBytes(1L)
        };
        user.Account = account;

        await _context.Users.AddAsync(user);
        await _context.Accounts.AddAsync(account);
        await _context.SaveChangesAsync();

        _passwordHasher.VerifyPassword("password", "hashed").Returns(true);

        var result = await _sut.LoginAsync(new LoginRequest { Email = "john@test.com", Password = "password" });

        result.Message.Should().Be("Login successful");
        result.AccessToken.Should().NotBeNullOrEmpty();

        await _auditService.Received(1).LogAsync("User Login", Arg.Any<string>(), user.Id.ToString());
    }

    [Fact]
    public async Task LoginAsync_Should_Throw_When_User_Not_Found()
    {
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _sut.LoginAsync(new LoginRequest { Email = "none@test.com", Password = "pass" }));

        ex.Message.Should().Be("Invalid email or password");
    }

    [Fact]
    public async Task LoginAsync_Should_Throw_When_Password_Invalid()
    {
        var user = new User
        {
            Email = "john@test.com",
            PasswordHash = "hashed",
            FullName = "John",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            IsEmailVerified = true,
            LastLoginAt = DateTime.UtcNow
        };

        var account = new Account
        {
            AccountNumber = "1234567890",
            IsActive = true,
            User = user,
            Currency = "NGN",
            LastUpdated = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Balance = 0.0m,    
            RowVersion = BitConverter.GetBytes(1L)
        };
        user.Account = account;

        await _context.Users.AddAsync(user);
        await _context.Accounts.AddAsync(account);
        await _context.SaveChangesAsync();

        _passwordHasher.VerifyPassword("wrong", "hashed").Returns(false);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _sut.LoginAsync(new LoginRequest { Email = "john@test.com", Password = "wrong" }));

        ex.Message.Should().Be("Invalid email or password");
    }

    [Fact]
    public async Task RefreshTokenAsync_Should_Succeed_For_Valid_Token()
    {
        var user = new User
        {
            Email = "john@test.com",
            FullName = "John",
            PasswordHash = "hashed",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsActive = true,
            IsEmailVerified = true
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var token = new RefreshToken
        {
            Token = "valid-token",
            UserId = user.Id,
            User = user,
            ExpiryDate = DateTime.UtcNow.AddDays(1),
            IsRevoked = false,
            IsUsed = false
        };
        await _context.RefreshTokens.AddAsync(token);
        await _context.SaveChangesAsync();

        var result = await _sut.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = "valid-token" });

        result.Message.Should().Be("Token refreshed successfully");
        await _auditService.Received(1).LogAsync("Token Refresh", Arg.Any<string>(), user.Id.ToString());
    }

    [Fact]
    public async Task RefreshTokenAsync_Should_Throw_When_Invalid()
    {
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _sut.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = "invalid" }));

        ex.Message.Should().Be("Invalid refresh token");
    }

    [Fact]
    public async Task RevokeTokenAsync_Should_Return_True_When_Found()
    {
        var token = new RefreshToken { Token = "token", UserId = 1 };
        await _context.RefreshTokens.AddAsync(token);
        await _context.SaveChangesAsync();

        var result = await _sut.RevokeTokenAsync(new RevokeTokenRequest { RefreshToken = "token" }, 1);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task LogoutAsync_AllDevices_Should_Revoke_All()
    {
        await _context.RefreshTokens.AddAsync(new RefreshToken { UserId = 1, IsRevoked = false });
        await _context.RefreshTokens.AddAsync(new RefreshToken { UserId = 1, IsRevoked = false });
        await _context.SaveChangesAsync();

        var result = await _sut.LogoutAsync(new LogoutRequest { AllDevices = true }, 1);

        result.Should().BeTrue();
        (await _context.RefreshTokens.AllAsync(x => x.IsRevoked)).Should().BeTrue();
    }
}