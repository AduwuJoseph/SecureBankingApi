using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using BankingAPI.Application.Interfaces;

namespace BankingAPI.Api.Filters;

public class IdempotencyFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKeyValues))
        {
            await next();
            return;
        }

        var idempotencyKey = idempotencyKeyValues.ToString();
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            await next();
            return;
        }

        var idempotencyService = context.HttpContext.RequestServices.GetRequiredService<IIdempotencyService>();

        // Add idempotency key to context for use in services
        context.HttpContext.Items["IdempotencyKey"] = idempotencyKey;

        await next();
    }
}