using BankingAPI.Application.DTOs.Auth;
using BankingAPI.Application.Interfaces;
using BankingAPI.Application.Services;
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

public class AuthServiceTests 
{
    private readonly IBankingDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    private readonly IAuditService _auditService;
    private readonly AuthService _authService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthServiceTests()
    {
        _context = Substitute.For<IBankingDbContext>();
        _configuration = Substitute.For<IConfiguration>();
        _logger = Substitute.For<ILogger<AuthService>>();
        _auditService = Substitute.For<IAuditService>();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _authService = new AuthService(_context, _configuration, _logger, _auditService, _httpContextAccessor, _passwordHasher);

        SetupConfiguration();
    }

    private void SetupConfiguration()
    {
        var jwtSettings = new Dictionary<string, string>
        {
            ["Secret"] = "TestSecretKeyForJWTTokenGeneration1234567890!@#$%",
            ["Issuer"] = "TestIssuer",
            ["Audience"] = "TestAudience",
            ["ExpirationMinutes"] = "60"
        };

        _configuration.GetSection("JwtSettings").Returns(new ConfigurationBuilder()
            .AddInMemoryCollection(jwtSettings)
            .Build()
            .GetSection("JwtSettings"));
    }

    [Fact]
    public async Task RegisterAsync_ValidUser_ShouldSucceed()
    {
        // Arrange
        var registerDto = new RegisterRequest
        {
            FullName = "Test User",
            Email = "test@example.com",
            Password = "password123",
            ConfirmPassword = "password123"
        };

        // Act
        var result = await _authService.RegisterAsync(registerDto);

        // Assert
        result.Message.Should().Be("Registration successful");
        result.User.UserId.Should().Be(1);
        result.User.Email.Should().Be("test@example.com");

        // Verify user was created
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == "test@example.com");
        user.Should().NotBeNull();

        // Verify account was created
        var account = await _context.Accounts.FindAsync(user!.Id);
        account.Should().NotBeNull();
        account!.Balance.Should().Be(0);

        // Verify audit log was called
        await _auditService.Received(1).LogAsync(
            Arg.Is<string>(s => s == "User Registration"),
            Arg.Is<string>(s => s.Contains("test@example.com")),
            Arg.Is<string>(s => s == "1"));
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ShouldThrowBusinessRuleException()
    {
        // Arrange
        var registerDto = new RegisterRequest
        {
            FullName = "Test User",
            Email = "test@example.com",
            Password = "password123",
            ConfirmPassword = "password123"
        };

        await _authService.RegisterAsync(registerDto);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _authService.RegisterAsync(registerDto));

        exception.Message.Should().Be("User with this email already exists");
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ShouldSucceed()
    {
        // Arrange
        var registerDto = new RegisterRequest
        {
            FullName = "Test User",
            Email = "test@example.com",
            Password = "password123",
            ConfirmPassword = "password123"
        };
        await _authService.RegisterAsync(registerDto);

        var loginDto = new LoginRequest
        {
            Email = "test@example.com",
            Password = "password123"
        };

        // Act
        var result = await _authService.LoginAsync(loginDto);

        // Assert
        result.Message.Should().Be("Login successful");
        result.AccessToken.Should().NotBeNullOrEmpty();

        // Verify audit log was called
        await _auditService.Received(1).LogAsync(
            Arg.Is<string>(s => s == "User Login"),
            Arg.Is<string>(s => s.Contains("test@example.com")),
            Arg.Is<string>(s => s == "1"));
    }

    [Fact]
    public async Task LoginAsync_InvalidEmail_ShouldThrowUnauthorizedException()
    {
        // Arrange
        var loginDto = new LoginRequest
        {
            Email = "nonexistent@example.com",
            Password = "password123"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _authService.LoginAsync(loginDto));

        exception.Message.Should().Be("Invalid email or password");
    }

    [Fact]
    public async Task LoginAsync_InvalidPassword_ShouldThrowUnauthorizedException()
    {
        // Arrange
        var registerDto = new RegisterRequest
        {
            FullName = "Test User",
            Email = "test@example.com",
            Password = "password123",
            ConfirmPassword = "password123"
        };
        await _authService.RegisterAsync(registerDto);

        var loginDto = new LoginRequest
        {
            Email = "test@example.com",
            Password = "wrongpassword"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _authService.LoginAsync(loginDto));

        exception.Message.Should().Be("Invalid email or password");
    }
}