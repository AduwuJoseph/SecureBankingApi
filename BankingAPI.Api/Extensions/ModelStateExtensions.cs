using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace BankingAPI.Api.Extensions
{
    public static class ModelStateExtensions
    {
        public static List<string> GetErrorMessages(this ModelStateDictionary modelState)
        {
            var errors = modelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return errors;
        }
    }
}
