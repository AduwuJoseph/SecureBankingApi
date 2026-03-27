

namespace BankingAPI.Domain.Exceptions
{
    public class NotFoundException : Exception
    {
        public NotFoundException(string message) : base(message) { }
    }

    public class ConcurrencyException : Exception
    {
        public ConcurrencyException(string message) : base(message) { }
    }

    public class InsufficientFundsException : Exception
    {
        public InsufficientFundsException(string message) : base(message) { }
    }

    public class ValidationException : Exception
    {
        public object Errors { get; set; }

        public ValidationException(string message) : base(message) { }

        public ValidationException(string message, object errors) : base(message)
        {
            Errors = errors;
        }
    }
}