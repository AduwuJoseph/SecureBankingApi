using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BankingAPI.Application.DTOs.Transfer
{
    /// <summary>
    /// Represents the result of a validation operation
    /// </summary>
    public class ValidationResult
    {
        private readonly List<ValidationError> _errors = new();
        private readonly List<string> _warnings = new();

        [JsonPropertyName("isValid")]
        public bool IsValid => !_errors.Any();

        [JsonPropertyName("errors")]
        public IReadOnlyList<ValidationError> Errors => _errors.AsReadOnly();

        [JsonPropertyName("warnings")]
        public IReadOnlyList<string> Warnings => _warnings.AsReadOnly();

        [JsonPropertyName("fee")]
        public decimal? Fee { get; set; }

        [JsonPropertyName("totalAmount")]
        public decimal? TotalAmount { get; set; }

        public void AddError(string message, string? code = null)
        {
            _errors.Add(new ValidationError(message, code));
        }

        public void AddWarning(string message)
        {
            _warnings.Add(message);
        }

        public void ClearErrors()
        {
            _errors.Clear();
        }

        public void ClearWarnings()
        {
            _warnings.Clear();
        }
    }

    /// <summary>
    /// Represents a validation error with optional error code
    /// </summary>
    public class ValidationError
    {
        public ValidationError(string message, string? code = null)
        {
            Message = message;
            Code = code;
        }

        [JsonPropertyName("message")]
        public string Message { get; }

        [JsonPropertyName("code")]
        public string? Code { get; }
    }
}
