using ProjectManager.API.Common.Options;
using Resend;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.OpenTelemetry;
using ProjectManager.API.Extensions;

// A .env betöltése MINDEN MÁS ELŐTT, a naplózás konfigurálását is megelőzve.
// Serilog sinkje a betöltési hitelesítő adatokat környezeti változóból olvassa tehát már most szükség van az .env-re.
var aspnetEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

if (!string.Equals(aspnetEnvironment, "Production", StringComparison.OrdinalIgnoreCase))
{
    var envFile = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", ".env");

    if (File.Exists(envFile))
    {
        foreach (var line in File.ReadAllLines(envFile))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

            var parts = line.Split('=', 2);
            if (parts.Length == 2)
                Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
        }
    }
}

// Serilog konfiguráció - legelső dolog
// A naplók az OpenObserve aggregátorba mennek, OTLP-n.
// A csomag neve ellenére ez NEM OpenTelemetry-bevezetés. Az OTLP itt csak az szállítási formátum.
var otlpEndpoint = Environment.GetEnvironmentVariable("OTLP_LOGS_ENDPOINT")
    ?? "http://localhost:5080/api/default/v1/logs";

// A Seq hitelesítés nélkül fogadta a logokat, az OpenObserve nem.
// Szándékosan külön betöltő felhasználó, nem a root: így az aggregátor admin jelszava nem kerül ebbe a konténerbe.
var ingestUser = Environment.GetEnvironmentVariable("OO_INGEST_USER");
var ingestToken = Environment.GetEnvironmentVariable("OO_INGEST_TOKEN");

// A Serilog alapértelmezésben ELNYELI a sinkjei hibáit:
// egy elutasított betöltés (rossz token -> HTTP 401) nyom nélkül eltűnik, és a naplók csendben elvesznek.
// Naplózásnál ez a legrosszabb hibamód, mert pont akkor nem derül ki, amikor a naplóra szükség lenne.
// A SelfLog ezért a sink saját hibáit a hibakimenetre írja.
Serilog.Debugging.SelfLog.Enable(Console.Error);

Serilog.Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.OpenTelemetry(options =>
    {
        options.Endpoint = otlpEndpoint;
        options.Protocol = OtlpProtocol.HttpProtobuf;

        // Hitelesítő adat nélkül is elindulunk, és a hiány nem állítja meg az alkalmazást:
        // a konzolos log ilyenkor is megvan (azt a Dokploy felülete is mutatja).
        if (!string.IsNullOrWhiteSpace(ingestUser) && !string.IsNullOrWhiteSpace(ingestToken))
        {
            var basic = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes($"{ingestUser}:{ingestToken}"));

            options.Headers = new Dictionary<string, string>
            {
                ["Authorization"] = $"Basic {basic}"
            };
        }

        options.ResourceAttributes = new Dictionary<string, object>
        {
            ["service.name"] = "projectmanager-api",

            // Két replikával enélkül nem lenne megállapítható, melyik példány írta a sort.
            // Konténerben a MachineName a konténer azonosítója.
            ["service.instance.id"] = Environment.MachineName
        };
    })
    .CreateLogger();

// Hitelesítő adat nélkül az OpenObserve 401-et ad, tehát a naplók NEM jutnak el hozzá.
// Ezt induláskor ki kell mondani: a konzolos log működik, így a figyelmeztetés látszik.
if (string.IsNullOrWhiteSpace(ingestUser) || string.IsNullOrWhiteSpace(ingestToken))
{
    Serilog.Log.Warning(
        "A naplo-aggregator hitelesito adatai nincsenek beallitva (OO_INGEST_USER / "
        + "OO_INGEST_TOKEN), ezert a naplok CSAK a konzolra kerulnek - az OpenObserve "
        + "a hitelesites nelkuli betoltest elutasitja. Vegpont: {Endpoint}", otlpEndpoint);
}

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Configuration.AddEnvironmentVariables();

    // Environment variables kinyerése
    var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
        ?? throw new InvalidOperationException("JWT_SECRET nincs beállítva!");

    //Fail-fast a gyenge titokra: a HMAC-SHA256 kulcsa 256 bitnél rövidebb ne legyen.
    //Enélkül a hiba csak az első bejelentkezéskor, futásidőben derülne ki:
    //ugyanaz a minta, amit az ENCRYPTION_KEY-nél már alkalmazva van.
    if (System.Text.Encoding.UTF8.GetByteCount(jwtSecret) < 32)
        throw new InvalidOperationException(
            "A JWT_SECRET legalább 32 bájt hosszú legyen (HMAC-SHA256 kulcsméret)!");

    var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER")
        ?? throw new InvalidOperationException("JWT_ISSUER nincs beállítva!");
    var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE")
        ?? throw new InvalidOperationException("JWT_AUDIENCE nincs beállítva!");
    var jwtExpiryMinutes = Environment.GetEnvironmentVariable("JWT_EXPIRY_MINUTES")
        ?? throw new InvalidOperationException("JWT_EXPIRY_MINUTES nincs beállítva!");
    var jwtRefreshTokenLifetime = Environment.GetEnvironmentVariable("JWT_REFRESH_TOKEN_LIFETIME")
        ?? throw new InvalidOperationException("JWT_REFRESH_TOKEN_LIFETIME nincs beállítva!");
    var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
        ?? throw new InvalidOperationException("DATABASE_URL nincs beállítva!");
    var encryptionKey = Environment.GetEnvironmentVariable("ENCRYPTION_KEY")
        ?? throw new InvalidOperationException("ENCRYPTION_KEY nincs beállítva!");
    var resendApiKey = Environment.GetEnvironmentVariable("RESEND_API_KEY");
    var emailFrom = Environment.GetEnvironmentVariable("EMAIL_FROM") ?? "noreply@trunkpeter.com";
    var redisConnection = Environment.GetEnvironmentVariable("REDIS_CONNECTION");
    var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "http://localhost:5173";
    var apiBaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL") ?? "http://localhost:5178";

    // MinIO
    var minioEndpoint = Environment.GetEnvironmentVariable("MINIO_ENDPOINT")
        ?? throw new InvalidOperationException("MINIO_ENDPOINT nincs beállítva!");
    var minioAccessKey = Environment.GetEnvironmentVariable("MINIO_ACCESS_KEY")
        ?? throw new InvalidOperationException("MINIO_ACCESS_KEY nincs beállítva!");
    var minioSecretKey = Environment.GetEnvironmentVariable("MINIO_SECRET_KEY")
        ?? throw new InvalidOperationException("MINIO_SECRET_KEY nincs beállítva!");
    var minioBucket = Environment.GetEnvironmentVariable("MINIO_BUCKET")
        ?? throw new InvalidOperationException("MINIO_BUCKET nincs beállítva!");
    var minioUseSSL = Environment.GetEnvironmentVariable("MINIO_USE_SSL") == "true";
    var minioPublicUrl = Environment.GetEnvironmentVariable("MINIO_PUBLIC_URL");

    // Attachment
    var maxUploadSizeMb = int.Parse(
        Environment.GetEnvironmentVariable("MAX_UPLOAD_SIZE_MB") ?? "64");

    //OrphanCleanupJob
    var orphanCleanupIntervalHours = int.Parse(
        Environment.GetEnvironmentVariable("ORPHAN_CLEANUP_INTERVAL_HOURS") ?? "24");

    //TokenCleanupJob
    var tokenCleanupIntervalHours = int.Parse(
        Environment.GetEnvironmentVariable("TOKEN_CLEANUP_INTERVAL_HOURS") ?? "6");
    var refreshTokenRetentionDays = int.Parse(
        Environment.GetEnvironmentVariable("REFRESH_TOKEN_RETENTION_DAYS") ?? "30");
    var confirmedUploadLogRetentionDays = int.Parse(
        Environment.GetEnvironmentVariable("CONFIRMED_UPLOAD_LOG_RETENTION_DAYS") ?? "90");

    // Options regisztrálás

    //JWT
    builder.Services.Configure<JwtOptions>(options =>
    {
        options.Secret = jwtSecret;
        options.Issuer = jwtIssuer;
        options.Audience = jwtAudience;
        options.ExpiryMinutes = int.Parse(jwtExpiryMinutes);
        options.RefreshTokenLifetimeMinutes = int.Parse(jwtRefreshTokenLifetime);
    });

    //Base URL
    builder.Services.Configure<ApiOptions>(options =>
    {
        options.BaseUrl = apiBaseUrl;
    });

    //Refresh token süti. A COOKIE_DOMAIN üresen hagyva host-only sütit ad - fejlesztői környezetben ez a helyes viselkedés.
    builder.Services.Configure<ProjectManager.API.Common.Options.CookieOptions>(options =>
    {
        options.Domain = Environment.GetEnvironmentVariable("COOKIE_DOMAIN");
    });

    //DB
    builder.Services.Configure<DatabaseOptions>(options =>
    {
        options.ConnectionString = connectionString;
    });

    //EMAIL
    builder.Services.Configure<EmailOptions>(options =>
    {
        options.ResendApiKey = resendApiKey;
        options.EmailFrom = emailFrom;
        options.FrontendUrl = frontendUrl;
    });

    //MiniO
    builder.Services.Configure<MinioOptions>(options =>
    {
        options.Endpoint = minioEndpoint;
        options.AccessKey = minioAccessKey;
        options.SecretKey = minioSecretKey;
        options.Bucket = minioBucket;
        options.UseSSL = minioUseSSL;
        options.PublicUrl = minioPublicUrl;
    });

    //Redis
    builder.Services.Configure<RedisOptions>(options =>
    {
        options.ConnectionString = redisConnection;
    });

    //Encryption
    builder.Services.Configure<EncryptionOptions>(options =>
    {
        options.Key = encryptionKey;
    });

    //Attachment
    builder.Services.Configure<AttachmentOptions>(options =>
    {
        options.MaxUploadSizeMb = maxUploadSizeMb;
    });

    //Takarító háttérfeladatok
    builder.Services.Configure<CleanupOptions>(options =>
    {
        options.OrphanCleanupIntervalHours = orphanCleanupIntervalHours;
        options.TokenCleanupIntervalHours = tokenCleanupIntervalHours;
        options.RefreshTokenRetentionDays = refreshTokenRetentionDays;
        options.ConfirmedUploadLogRetentionDays = confirmedUploadLogRetentionDays;
    });

    // Service Registration (DI Container)
    builder.Host.UseSerilog();
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddDatabase(connectionString);
    builder.Services.AddJwtAuthentication(new JwtOptions
    {
        Secret = jwtSecret,
        Issuer = jwtIssuer,
        Audience = jwtAudience,
        ExpiryMinutes = int.Parse(jwtExpiryMinutes),
        RefreshTokenLifetimeMinutes = int.Parse(jwtRefreshTokenLifetime)
    });
    builder.Services.AddRedisAndSignalR(redisConnection);
    builder.Services.AddSwagger();
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", policy =>
        {
            policy.WithOrigins(frontendUrl)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    });
    builder.Services.AddEmailService(new EmailOptions
    {
        ResendApiKey = resendApiKey,
        EmailFrom = emailFrom,
        FrontendUrl = frontendUrl
    });
    builder.Services.AddRbac();
    builder.Services.AddApplicationServices();

    // Build
    var app = builder.Build();

    // Middleware Pipeline - Sorrendjük kritikus
    app.UseProjectManagerMiddleware();

    // DB migráció + webhook secret migráció
    await app.RunMigrationsAsync();
    await app.MigrateWebhookSecretsAsync();
    await app.EncryptExistingTotpSecretsAsync();

    //A regisztráció ehhez a sorhoz köti az elfogadást, ezért a migráció után azonnal fut
    await app.SeedTermsVersionAsync();

    // Start
    Serilog.Log.Information("Alkalmazás indul!");
    app.Run();
}
catch (Exception ex)
{
    Serilog.Log.Fatal(ex, "Az alkalmazás váratlanul leállt!");
}
finally
{
    Serilog.Log.CloseAndFlush();
}