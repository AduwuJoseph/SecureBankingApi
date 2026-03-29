using BankingAPI.Application.DTOs;
using BankingAPI.Application.DTOs.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingAPI.Application.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponse> RegisterAsync(RegisterRequest registerRequest, string? deviceInfo = null, string? ipAddress = null);
        Task<AuthResponse> LoginAsync(LoginRequest loginRequest, string? deviceInfo = null, string? ipAddress = null);
        Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest refreshTokenDto, string? deviceInfo = null, string? ipAddress = null);
        Task<bool> RevokeTokenAsync(RevokeTokenRequest revokeTokenDto, int userId);
        Task<bool> LogoutAsync(LogoutRequest logoutDto, int userId);
        Task<IEnumerable<RefreshTokenInfoResponse>> GetUserRefreshTokensAsync(int userId);
        Task<bool> RevokeAllUserTokensAsync(int userId, string? reason = null);
    }
}
