namespace TicketWebApp.Middleware
{
    public class CorrelationIdMiddleware //purpose Attach a unique ID to every request
    {
        private const string HeaderName = "X-Correlation-Id"; 
        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            var incoming = context.Request.Headers[HeaderName].FirstOrDefault(); // Did client already send an ID?

            var id = string.IsNullOrWhiteSpace(incoming) //if no id create new unique id else use existing one
                ? Guid.NewGuid().ToString("N")
                : incoming;
            //store id
            context.TraceIdentifier = id; //builtin tracking
            context.Items["CorrelationId"] = id; //shared data inside request
            //send id back to client
            context.Response.Headers[HeaderName] = id; //client recieves same id

            await _next(context); //call next middleware
        }
    }
}