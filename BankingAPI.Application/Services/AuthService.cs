// BankingAPI.Application/Services/AuthService.cs
using BankingAPI.Application.DTOs;
using BankingAPI.Application.DTOs.Account;
using BankingAPI.Application.DTOs.Auth;
using BankingAPI.Application.DTOs.Responses;
using BankingAPI.Application.Interfaces;
using BankingAPI.Domain.Common;
using BankingAPI.Domain.Entities;
using BankingAPI.Domain.Exceptions;
using BankingAPI.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;
        private readonly IPasswordHasher _passwordHasher;

        public AuthService(
            IUnitOfWork unitOfWork,
            IConfiguration configuration,
            ILogger<AuthService> logger,
            IPasswordHasher passwordHasher)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
            _logger = logger;
            _passwordHasher = passwordHasher;
        }

        public async Task<ApiResponse<AuthResponse>> RegisterAsync(RegisterRequest registerRequest)
        {
            // Check if email already exists
            var existingUser = await _unitOfWork.Users.GetByEmailAsync(registerRequest.Email);
            if (existingUser != null)
                throw new ValidationException("Email already registered");

            // Begin transaction for user creation
            await _unitOfWork.BeginTransactionAsync();

            try
            {
                // Create user
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    FullName = registerRequest.FullName,
                    Email = registerRequest.Email.ToLower(),
                    PasswordHash = _passwordHasher.HashPassword(registerRequest.Password),
                    PhoneNumber = registerRequest.PhoneNumber,
                    IsEmailVerified = false,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.Users.AddAsync(user);

                // Create account for user
                var account = new Account
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    AccountNumber = await GenerateUniqueAccountNumber(),
                    Balance = 0,
                    Currency = "USD",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.Accounts.AddAsync(account);
                await _unitOfWork.CompleteAsync();
                await _unitOfWork.CommitTransactionAsync();

                _logger.LogInformation("New user registered: {Email} with account: {AccountNumber}",
                    user.Email, account.AccountNumber);

                // Generate tokens
                var tokens = await GenerateTokens(user);

                return new ApiResponse<AuthResponse>
                {
                    Data = new AuthResponse
                    {
                        Token = tokens.AccessToken,
                        RefreshToken = tokens.RefreshToken,
                        ExpiresAt = DateTime.UtcNow.AddMinutes(GetJwtExpiryMinutes()),
                        User = new UserResponse
                        {
                            Id = user.Id,
                            FullName = user.FullName,
                            Email = user.Email,
                            PhoneNumber = user.PhoneNumber,
                            Account = new AccountResponse
                            {
                                Id = account.Id,
                                AccountNumber = account.AccountNumber,
                                Balance = account.Balance,
                                Currency = account.Currency
                            }
                        }
                    },
                    Code = ApiResponseCodes.Success,
                    Message = "Registration successful"
                };
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                _logger.LogError(ex, "Registration failed for email: {Email}", registerRequest.Email);
                throw;
            }
        }

        public async Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest loginRequest)
        {
            var user = await _unitOfWork.Users.GetByEmailAsync(loginRequest.Email.ToLower());

            if (user == null)
                throw new UnauthorizedAccessException("Invalid credentials");

            if (!user.IsActive)
                throw new UnauthorizedAccessException("Account is deactivated");

            // Verify password
            if (!_passwordHasher.VerifyPassword(user.PasswordHash, loginRequest.Password))
                throw new UnauthorizedAccessException("Invalid credentials");

            // Update last login
            user.LastLoginAt = DateTime.UtcNow;
            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("User logged in: {Email}", user.Email);

            // Generate tokens
            var tokens = await GenerateTokens(user);

            // Get user account
            var account = await _unitOfWork.Accounts.GetAccountWithUserAsync(user.Id);

            return new ApiResponse<AuthResponse>
            {
                Data = new AuthResponse
                {
                    Token = tokens.AccessToken,
                    RefreshToken = tokens.RefreshToken,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(GetJwtExpiryMinutes()),
                    User = new UserResponse
                    {
                        Id = user.Id,
                        FullName = user.FullName,
                        Email = user.Email,
                        PhoneNumber = user.PhoneNumber,
                        Account = account != null ? new AccountResponse
                            {
                                Id = account.Id,
                                AccountNumber = account.AccountNumber,
                                Balance = account.Balance,
                                Currency = account.Currency
                            } : null
                    }
                },
                Code = ApiResponseCodes.Success,
                Message = "Login successful"
            };
        }

        public async Task LogoutAsync(string token)
        {
            // Add token to blacklist (if implementing token blacklist)
            // For now, just log the logout
            _logger.LogInformation("User logged out");
            await Task.CompletedTask;
        }

        public async Task<AuthResponseDto> RefreshTokenAsync(string refreshToken)
        {
            // Validate refresh token
            var principal = GetPrincipalFromExpiredToken(refreshToken);
            if (principal == null)
                throw new UnauthorizedAccessException("Invalid refresh token");

            var email = principal.FindFirst(ClaimTypes.Email)?.Value;
            var user = await _unitOfWork.Users.GetByEmailAsync(email);

            if (user == null || !user.IsActive)
                throw new UnauthorizedAccessException("Invalid refresh token");

            // Generate new tokens
            var tokens = await GenerateTokens(user);

            var account = await _unitOfWork.Accounts.GetAccountWithUserAsync(user.Id);

            return new AuthResponseDto
            {
                Token = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(GetJwtExpiryMinutes()),
                User = new UserDto
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    Account = account != null ? new AccountDto
                    {
                        Id = account.Id,
                        AccountNumber = account.AccountNumber,
                        Balance = account.Balance,
                        Currency = account.Currency
                    } : null
                }
            };
        }

        public async Task<bool> VerifyEmailAsync(string token)
        {
            // Implement email verification logic
            // This would typically validate a token sent via email
            await Task.CompletedTask;
            return true;
        }

        private async Task<(string AccessToken, string RefreshToken)> GenerateTokens(User user)
        {
            var accessToken = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();

            // Store refresh token (you might want to store in database)
            // For now, we'll just return it

            return (accessToken, refreshToken);
        }

        private string GenerateJwtToken(User user)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Name, user.FullName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("user_id", user.Id.ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.UtcNow.AddMinutes(GetJwtExpiryMinutes());

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: expires,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        private ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"])),
                ValidateLifetime = false // Don't validate lifetime for refresh token
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);

            if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256,
                    StringComparison.InvariantCultureIgnoreCase))
                throw new SecurityTokenException("Invalid token");

            return principal;
        }

        private int GetJwtExpiryMinutes()
        {
            return int.Parse(_configuration["Jwt:ExpiryInMinutes"] ?? "60");
        }

        private async Task<string> GenerateUniqueAccountNumber()
        {
            string accountNumber;
            bool exists;

            do
            {
                accountNumber = GenerateAccountNumber();
                exists = await _unitOfWork.Accounts.ExistsAsync(a => a.AccountNumber == accountNumber);
            }
            while (exists);

            return accountNumber;
        }

        private string GenerateAccountNumber()
        {
            var random = new Random();
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var randomPart = random.Next(100000, 999999).ToString();
            return $"ACC{timestamp}{randomPart}".Substring(0, 20);
        }
    }
}