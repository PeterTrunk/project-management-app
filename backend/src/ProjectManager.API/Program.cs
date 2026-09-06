using ProjectManager.API.Common.Options;
using Resend;
using Serilog;
using Serilog.Events;
using ProjectManager.API.Extensions;

// Serilog konfiguráció - legelső dolog
Serilog.Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.Seq(Environment.GetEnvironmentVariable("SEQ_URL") ?? "http://localhost:5341")
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // .env fájl betöltése CSAK development-ben
    if (!builder.Environment.IsProduction())
    {
        var envFile = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..",
            ".env"
        );
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

    //OrphanCleanupJob
    builder.Services.Configure<CleanupOptions>(options =>
    {
        options.OrphanCleanupIntervalHours = orphanCleanupIntervalHours;
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