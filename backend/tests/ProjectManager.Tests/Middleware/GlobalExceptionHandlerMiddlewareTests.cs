using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectManager.API.Common.Exceptions;
using ProjectManager.API.Middleware;
using System.Text;
using System.Text.Json;

namespace ProjectManager.Tests.Middleware
{
    /// <summary>
    /// A middleware a hibaüzenet-szivárgás utolsó védvonala: ami nem AppException, annak az
    /// üzenete (EF Core, Npgsql, MinIO SDK) sosem hagyhatja el a szervert.
    /// </summary>
    public class GlobalExceptionHandlerMiddlewareTests
    {
        private const string GenericMessage = "Belső szerverhiba történt!";

        private static GlobalExceptionHandlerMiddleware CreateSut(RequestDelegate next) =>
            new(next, NullLogger<GlobalExceptionHandlerMiddleware>.Instance);

        private static GlobalExceptionHandlerMiddleware CreateSutThrowing(Exception ex) =>
            CreateSut(_ => throw ex);

        private static DefaultHttpContext CreateContext(MemoryStream body)
        {
            var context = new DefaultHttpContext();
            context.Response.Body = body;
            context.Request.Method = "POST";
            context.Request.Path = "/api/projects/tasks";
            return context;
        }

        private static string ReadErrorField(MemoryStream body)
        {
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(body.ToArray()));
            return doc.RootElement.GetProperty("error").GetString()!;
        }

        //Kezelt kivételek

        public static TheoryData<Type, int> AppExceptions => new()
        {
            { typeof(ValidationException), StatusCodes.Status400BadRequest },
            { typeof(ForbiddenException), StatusCodes.Status403Forbidden },
            { typeof(NotFoundException), StatusCodes.Status404NotFound },
            { typeof(ConflictException), StatusCodes.Status409Conflict },
            { typeof(RateLimitException), StatusCodes.Status429TooManyRequests }
        };

        [Theory]
        [MemberData(nameof(AppExceptions))]
        public async Task AppException_WritesItsOwnStatusCode(Type exceptionType, int expectedStatus)
        {
            var ex = (Exception)Activator.CreateInstance(exceptionType, "A task nem található!")!;
            using var body = new MemoryStream();
            var context = CreateContext(body);

            await CreateSutThrowing(ex).InvokeAsync(context);

            Assert.Equal(expectedStatus, context.Response.StatusCode);
        }

        [Theory]
        [MemberData(nameof(AppExceptions))]
        public async Task AppException_MessageReachesTheClient(Type exceptionType, int _)
        {
            var ex = (Exception)Activator.CreateInstance(exceptionType, "A task nem található!")!;
            using var body = new MemoryStream();
            var context = CreateContext(body);

            await CreateSutThrowing(ex).InvokeAsync(context);

            Assert.Equal("A task nem található!", ReadErrorField(body));
        }

        [Fact]
        public async Task AppException_WritesJsonContentType()
        {
            using var body = new MemoryStream();
            var context = CreateContext(body);

            await CreateSutThrowing(new NotFoundException("nincs meg")).InvokeAsync(context);

            Assert.Equal("application/json", context.Response.ContentType);
        }

        [Fact]
        public async Task AppException_WrapsTheMessageInAnErrorProperty()
        {
            using var body = new MemoryStream();
            var context = CreateContext(body);

            await CreateSutThrowing(new ValidationException("hibás adat")).InvokeAsync(context);

            //A frontend axios interceptora erre az alakra épül
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(body.ToArray()));
            Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("error").ValueKind);
            Assert.Single(doc.RootElement.EnumerateObject());
        }

        //Kezeletlen kivételek

        public static TheoryData<Exception> LeakyExceptions => new()
        {
            new InvalidOperationException("relation project_tasks does not exist"),
            new NullReferenceException("Object reference not set to an instance of an object."),
            new TimeoutException("Npgsql connection pool exhausted at 10.0.0.5:5432"),
            new ArgumentException("Value cannot be null. (Parameter connectionString)")
        };

        [Theory]
        [MemberData(nameof(LeakyExceptions))]
        public async Task UnhandledException_Returns500(Exception ex)
        {
            using var body = new MemoryStream();
            var context = CreateContext(body);

            await CreateSutThrowing(ex).InvokeAsync(context);

            Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        }

        [Theory]
        [MemberData(nameof(LeakyExceptions))]
        public async Task UnhandledException_NeverLeaksTheOriginalMessage(Exception ex)
        {
            using var body = new MemoryStream();
            var context = CreateContext(body);

            await CreateSutThrowing(ex).InvokeAsync(context);

            var responseBody = Encoding.UTF8.GetString(body.ToArray());
            Assert.Equal(GenericMessage, ReadErrorField(body));
            Assert.DoesNotContain(ex.Message, responseBody);
        }

        [Fact]
        public async Task UnhandledException_DoesNotLeakTheStackTrace()
        {
            using var body = new MemoryStream();
            var context = CreateContext(body);

            //Valódi, feldobott kivétel - ennek már van stack trace-e
            Exception thrown;
            try { throw new InvalidOperationException("belső részlet"); }
            catch (Exception ex) { thrown = ex; }

            await CreateSutThrowing(thrown).InvokeAsync(context);

            var responseBody = Encoding.UTF8.GetString(body.ToArray());
            Assert.DoesNotContain("InvalidOperationException", responseBody);
            Assert.DoesNotContain(nameof(GlobalExceptionHandlerMiddlewareTests), responseBody);
        }

        //Az AppException üzenete kimehet, de a belső kivétel részletei nem
        [Fact]
        public async Task AppException_DoesNotLeakTheInnerException()
        {
            using var body = new MemoryStream();
            var context = CreateContext(body);
            var ex = new ConflictException("A sor időközben módosult!");

            await CreateSutThrowing(ex).InvokeAsync(context);

            Assert.Equal("A sor időközben módosult!", ReadErrorField(body));
            Assert.DoesNotContain("40001", Encoding.UTF8.GetString(body.ToArray()));
        }

        //Boldog ág

        [Fact]
        public async Task NoException_LeavesTheResponseUntouched()
        {
            using var body = new MemoryStream();
            var context = CreateContext(body);
            context.Response.StatusCode = StatusCodes.Status201Created;

            await CreateSut(_ => Task.CompletedTask).InvokeAsync(context);

            Assert.Equal(StatusCodes.Status201Created, context.Response.StatusCode);
            Assert.Empty(body.ToArray());
        }

        //Már elindult válasz

        [Fact]
        public async Task ResponseAlreadyStarted_AbortsInsteadOfWriting()
        {
            var lifetime = new RecordingLifetimeFeature();
            var context = new DefaultHttpContext();
            context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());
            context.Features.Set<IHttpRequestLifetimeFeature>(lifetime);
            context.Request.Method = "GET";
            context.Request.Path = "/api/attachments/download";

            //Streamelt letöltés közben dobott hiba: a StatusCode írása itt már
            //InvalidOperationException-t okozna, ami elfedné az eredeti hibát
            await CreateSutThrowing(new NotFoundException("A fájl nem található!")).InvokeAsync(context);

            Assert.True(lifetime.Aborted);
        }

        [Fact]
        public async Task ResponseAlreadyStarted_DoesNotOverwriteTheStatusCode()
        {
            var response = new StartedResponseFeature { StatusCode = StatusCodes.Status200OK };
            var context = new DefaultHttpContext();
            context.Features.Set<IHttpResponseFeature>(response);
            context.Features.Set<IHttpRequestLifetimeFeature>(new RecordingLifetimeFeature());
            context.Request.Path = "/api/attachments/download";

            await CreateSutThrowing(new InvalidOperationException("streamelés közbeni hiba")).InvokeAsync(context);

            Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        }

        //A DefaultHttpContext beépített válasz-feature-jének a HasStarted értéke mindig hamis,
        //ezért az elindult válasz ágához saját feature kell.
        private sealed class StartedResponseFeature : IHttpResponseFeature
        {
            public int StatusCode { get; set; } = StatusCodes.Status200OK;
            public string? ReasonPhrase { get; set; }
            public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
            public Stream Body { get; set; } = Stream.Null;
            public bool HasStarted => true;

            public void OnStarting(Func<object, Task> callback, object state) { }
            public void OnCompleted(Func<object, Task> callback, object state) { }
        }

        private sealed class RecordingLifetimeFeature : IHttpRequestLifetimeFeature
        {
            public CancellationToken RequestAborted { get; set; }
            public bool Aborted { get; private set; }

            public void Abort() => Aborted = true;
        }
    }
}
