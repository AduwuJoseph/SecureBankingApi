namespace BankingAPI.Api.Middleware
{
    public class CorrelationIdMiddleware
    {
        public async Task Invoke(HttpContext context)
        {
            var correlationId = Guid.NewGuid().ToString();
            context.Response.Headers.Add("X-Correlation-ID", correlationId);
            await _next(context);
        }
    }
}
