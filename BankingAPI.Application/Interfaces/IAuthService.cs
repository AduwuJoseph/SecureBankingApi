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
        Task<ApiResponse<AuthResponse>> RegisterAsync(RegisterRequest registerRequest);
        Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest loginRequest);
        Task<ApiResponse> LogoutAsync(string token);
        Task<ApiResponse<AuthResponse>> RefreshTokenAsync(string refreshToken);
        Task<ApiResponse<bool>> VerifyEmailAsync(string token);
    }
}
