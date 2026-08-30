namespace ProjectManager.API.Middleware
{
    public class GlobalExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

        public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Kezeletlen kivétel | Method: {Method} | Path: {Path} | Type: {ExceptionType} | Message: {Message}",
                    context.Request.Method,
                    context.Request.Path,
                    ex.GetType().Name,
                    ex.Message);

                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\": \"Belső szerverhiba történt!\"}");
            }
        }
    }
}
