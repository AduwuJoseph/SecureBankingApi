using BankingAPI.Application.DTOs.Account;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BankingAPI.Application.DTOs.Auth
{
    public class AuthResponse
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }
        [JsonPropertyName("accessToken")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refreshToken")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expiresIn")]
        public int ExpiresIn { get; set; } = 3600; // 1 hour in seconds

        [JsonPropertyName("tokenType")]
        public string TokenType { get; set; } = "Bearer";

        [JsonPropertyName("refreshTokenExpiry")]
        public DateTime? RefreshTokenExpiry { get; set; }
        [JsonPropertyName("user")]
        public UserResponse User { get; set; }
    }
}
