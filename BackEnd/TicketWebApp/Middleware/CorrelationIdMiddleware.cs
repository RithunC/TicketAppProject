namespace TicketWebApp.Middleware
{
    public class CorrelationIdMiddleware
    {
        private const string HeaderName = "X-Correlation-Id";
        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            var incoming = context.Request.Headers[HeaderName].FirstOrDefault();

            var id = string.IsNullOrWhiteSpace(incoming)
                ? Guid.NewGuid().ToString("N")
                : incoming;

            context.TraceIdentifier = id;
            context.Items["CorrelationId"] = id;

            context.Response.Headers[HeaderName] = id;

            await _next(context);
        }
    }
}