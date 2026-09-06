using ProjectManager.API.Common.Exceptions;
using System.Text.Json;

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
            catch (AppException ex)
            {
                //Szándékosan a felhasználónak szánt kivétel:
                //az üzenete kimehet, és a saját státuszkódját kapja - nem minden 400-nak látszik
                _logger.LogWarning(
                    "Kezelt hiba | Method: {Method} | Path: {Path} | Type: {ExceptionType} | Status: {StatusCode} | Message: {Message}",
                    context.Request.Method,
                    context.Request.Path,
                    ex.GetType().Name,
                    ex.StatusCode,
                    ex.Message);

                await WriteErrorAsync(context, ex.StatusCode, ex.Message);
            }
            catch (Exception ex)
            {
                //Minden más: a részletek CSAK a naplóba mennek. Az ex.Message ilyenkor EF Core / Npgsql / MinIO belső szövege lehet,
                //ami a séma és az infrastruktúra részleteit adná ki
                _logger.LogError(ex,
                    "Kezeletlen kivétel | Method: {Method} | Path: {Path} | Type: {ExceptionType} | Message: {Message}",
                    context.Request.Method,
                    context.Request.Path,
                    ex.GetType().Name,
                    ex.Message);

                await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, "Belső szerverhiba történt!");
            }
        }

        private async Task WriteErrorAsync(HttpContext context, int statusCode, string message)
        {
            //Streamelt letöltésnél (AttachmentController) a fejlécek már kimehettek.
            //Ilyenkor a StatusCode írása InvalidOperationException-t dobna,
            //ami elfedné az eredeti hibát - a kapcsolatot inkább megszakítjuk.
            if (context.Response.HasStarted)
            {
                _logger.LogWarning(
                    "A válasz már elindult, a hibaválasz nem írható ki | Path: {Path}",
                    context.Request.Path);
                context.Abort();
                return;
            }

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = message }));
        }
    }
}
