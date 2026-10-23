using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using ProjectManager.API.Authorization.Handlers;
using ProjectManager.API.Authorization.Requirements;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.Common.Options;
using ProjectManager.API.Data;
using ProjectManager.API.Filters;
using ProjectManager.API.Services.ActivityService;
using ProjectManager.API.Services.AttachmentService;
using ProjectManager.API.Services.Auth;
using ProjectManager.API.Services.BackgroundJobs;
using ProjectManager.API.Services.BoardService;
using ProjectManager.API.Services.ColumnService;
using ProjectManager.API.Services.CommentService;
using ProjectManager.API.Services.CounterService;
using ProjectManager.API.Services.CurrentUserService;
using ProjectManager.API.Services.EmailService;
using ProjectManager.API.Services.EncryptionService;
using ProjectManager.API.Services.FileStorageService;
using ProjectManager.API.Services.GitService;
using ProjectManager.API.Services.GitWebhookService;
using ProjectManager.API.Services.GitWebhookService.Payloads;
using ProjectManager.API.Services.IntegrationService;
using ProjectManager.API.Services.LabelService;
using ProjectManager.API.Services.LexorankService;
using ProjectManager.API.Services.ProjectService;
using ProjectManager.API.Services.ProjectTaskService;
using ProjectManager.API.Services.RateLimit;
using ProjectManager.API.Services.SprintService;
using ProjectManager.API.Services.StatisticsService;
using ProjectManager.API.Services.TeamService;
using Resend;
using StackExchange.Redis;
using System.Reflection;
using System.Text;

namespace ProjectManager.API.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>A PostgreSQL `max_connections` beállítása. Lásd a docker-compose.prod.yml postgres `command`-ját.</summary>
        private const int PostgresMaxConnections = 100;

        /// <summary>
        /// Tartalék kapcsolatok, amelyeket az alkalmazás nem foglalhat le: a Dokploy mentés
        /// `pg_dump`-ja, egy üzemeltetői `psql` munkamenet, és az induláskor futó migráció.
        /// </summary>
        private const int ReservedConnections = 20;

        public static IServiceCollection AddDatabase(
            this IServiceCollection services,
            string connectionString,
            int apiReplicas)
        {
            //A kapcsolatkészletet a példányszámból származtatjuk, mert a korlát a példányokra
            //NÉZVE összegződik: az Npgsql alapértelmezett 100-as pool FOLYAMATONKÉNT értendő,
            //tehát két replika 200 kapcsolatot kérhetett egy 100-as max_connections ellen.
            //A 100 fölötti kérés `53300: too many clients already` hibát adna - ami az ÖSSZES
            //replikát érinti. Korlátos pool mellett a kimerülés egyetlen replikán jelentkezik
            //pool-timeoutként: lokális és újrapróbálható hiba a globális kiesés helyett.
            var builder = new NpgsqlConnectionStringBuilder(connectionString);

            //Ha az üzemeltető explicit értéket adott a DATABASE_URL-ben, az nyer: a
            //származtatás kényelmi alapérték, nem felülírhatatlan szabály.
            //
            //Az észlelés az ALAPÉRTÉKHEZ hasonlít, és nem a nyers szövegben keres. Így az
            //értelmezést az Npgsql végzi, tehát minden általa elfogadott írásmód beleesik
            //(`Maximum Pool Size`, `MaxPoolSize`, kis-nagybetűtől függetlenül), egy érvénytelen
            //kulcs pedig már a beolvasásnál `ArgumentException`-t dob - mindkettő mérve.
            //
            //A `ContainsKey` erre NEM használható: az Npgsql erősen típusos buildere minden
            //ismert kulcsot alapértékkel tartalmaz, ezért mindig igazat ad (mérve). Arra
            //épülve a származtatás soha nem futott volna le, és a pool korlátlan maradt volna.
            //
            //Egyetlen vakfolt: ha az üzemeltető épp az alapértéket (100) írja ki explicit módon.
            //Ezt felülírjuk - ami helyes is, hiszen pont ez a korlátlan érték, amit kötni akarunk.
            var defaultMaxPoolSize = new NpgsqlConnectionStringBuilder().MaxPoolSize;

            if (builder.MaxPoolSize == defaultMaxPoolSize)
            {
                //A 10-es alsó korlát azért kell, hogy sok replika mellett se maradjon
                //használhatatlanul kicsi a készlet.
                const int minimumPoolSize = 10;
                var derived = (PostgresMaxConnections - ReservedConnections) / Math.Max(1, apiReplicas);
                builder.MaxPoolSize = Math.Max(minimumPoolSize, derived);

                //Ha az alsó korlát lép életbe, a `replikaszám × pool + tartalék <= max_connections`
                //összefüggés MÁR NEM ÁLL - és pont ez volt a változtatás célja. Ezt kimondjuk,
                //mert különben csendben visszakapnánk a hibát, amit megszüntetni akartunk.
                //Ilyen példányszámnál a helyes válasz a max_connections emelése, nem a pool
                //további szűkítése: 10 alatt a készlet már a normál működést akadályozná.
                if (derived < minimumPoolSize)
                {
                    var required = (apiReplicas * minimumPoolSize) + ReservedConnections;
                    Serilog.Log.Warning(
                        "A DB kapcsolatkeszlet alsó korlátja ({MinimumPoolSize}) lépett életbe "
                        + "{ApiReplicas} példánynál, ezért a példányok együtt több kapcsolatot "
                        + "kérhetnek, mint a max_connections ({PostgresMaxConnections}). "
                        + "Emeld a postgres max_connections értékét legalább {Required}-ra "
                        + "(docker-compose: `postgres -c max_connections=...`), és írd át a "
                        + "PostgresMaxConnections állandót. {EventType}",
                        minimumPoolSize, apiReplicas, PostgresMaxConnections, required,
                        "DbPoolFloorReached");
                }
            }

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(builder.ConnectionString));
            return services;
        }

        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, JwtOptions jwtOptions)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtOptions.Issuer,
                        ValidAudience = jwtOptions.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(jwtOptions.Secret))
                    };

                    // SignalR JWT kezelés:
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];
                            var path = context.HttpContext.Request.Path;
                            if (!string.IsNullOrEmpty(accessToken) &&
                                path.StartsWithSegments("/hubs"))
                            {
                                context.Token = accessToken;
                            }
                            return Task.CompletedTask;
                        }
                    };
                });
            return services;
        }

        /// <summary>
        /// Kérelemkorlátozás regisztrálása a példányszámnak megfelelő megvalósítással.
        /// </summary>
        /// <remarks>
        /// A döntés a <c>API_REPLICAS</c>-on alapul, mert a kérelemkorlátozás helyessége
        /// a példányszámtól függ, nem attól, hogy be van-e állítva egy connection string:
        /// <list type="bullet">
        /// <item>egy példány: a memóriában vezetett számláló a teljes rendszert látja, tehát helyes;</item>
        /// <item>több példány: csak a megosztott Redis számláló helyes, különben minden példány
        /// külön számol, és a tényleges korlát a példányszámmal felszorozódik - CSENDBEN.</item>
        /// </list>
        /// Ezért több replikánál a Redis hiánya induláskor hibát dob. Korábban a Redis
        /// hiánya feltétel nélkül csendes hiba volt: éles módban az alkalmazás elindult,
        /// majd a teljes AuthController, az AttachmentService és a webhook végpont
        /// 500-at adott, mert az IConnectionMultiplexer nem volt feloldható.
        /// </remarks>
        public static IServiceCollection AddRateLimiting(
            this IServiceCollection services,
            string? redisConnection,
            int apiReplicas)
        {
            if (apiReplicas > 1)
            {
                if (string.IsNullOrWhiteSpace(redisConnection))
                    throw new InvalidOperationException(
                        $"A REDIS_CONNECTION kötelező, ha API_REPLICAS > 1 (most {apiReplicas}). " +
                        "Megosztott számláló nélkül a kérelemkorlátozás példányonként számolna, " +
                        "tehát a tényleges korlát a példányszámmal felszorozódna.");

                services.AddScoped<IRateLimitService, RateLimitService>();
                return services;
            }

            //Egy példány: a számláló a folyamat memóriájában is a teljes rendszert látja.
            //Singleton, nem Scoped - Scopedként minden kérés friss számlálót kapna, és a
            //korlát soha nem lépne életbe. A Redis-es változatnál ez nem gond, mert ott az
            //állapot a folyamaton kívül van.
            services.AddMemoryCache();
            services.AddSingleton<IRateLimitService, InMemoryRateLimitService>();
            return services;
        }

        public static IServiceCollection AddRedisAndSignalR(this IServiceCollection services, string? redisConnection)
        {
            //Explicit időzítés, hogy a kliens és a szerver ne mondhasson ellent egymásnak.
            //A szabály: a ClientTimeoutInterval legyen legalább kétszerese annak, amilyen gyakran a kliens pingel (VITE_SIGNALR_KEEPALIVE_SECONDS).
            //Az alapértelmezett 30 másodperc mellett egy 30 mp-nél ritkábban pingelő kliens kapcsolatát a szerver folyamatosan bontotta.
            //A kliens oldali érték maximuma ezért 30 mp.
            var signalRBuilder = services.AddSignalR(options =>
            {
                options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
            });
            //A backplane feltételes, és ez helyes: egyetlen példánynál a memóriában tartott
            //hub ugyanazt adja, mert nincs másik példány, akihez az üzenetet át kellene vinni.
            //Több példánynál viszont a Redis kötelező - ezt az AddRateLimiting kényszeríti ki,
            //ott egy helyen, hogy a két döntés ne tudjon ellentmondani egymásnak.
            if (!string.IsNullOrEmpty(redisConnection))
            {
                //SignalR backplane
                signalRBuilder.AddStackExchangeRedis(redisConnection, options =>
                {
                    options.Configuration.ChannelPrefix = RedisChannel.Literal("ProjectManager");
                });

                //A Redis-es kérelemkorlátozás multiplexere. A Connect() szinkron és eager:
                //ha a Redis még nem fogad kapcsolatot, itt RedisConnectionException-nel el sem
                //indul az alkalmazás (mérve). Ezért a compose-ban a pm-api a redisre
                //`condition: service_healthy`-vel vár, nem `service_started`-del.
                var multiplexer = ConnectionMultiplexer.Connect(redisConnection);
                services.AddSingleton<IConnectionMultiplexer>(multiplexer);
            }
            return services;
        }

        public static IServiceCollection AddSwagger(this IServiceCollection services)
        {
            services.AddSwaggerGen(options =>
            {
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                options.IncludeXmlComments(xmlPath, true);

                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Írd be: Bearer {token}"
                });
                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });
            return services;
        }

        public static IServiceCollection AddEmailService(this IServiceCollection services, EmailOptions emailOptions)
        {
            if (!string.IsNullOrEmpty(emailOptions.ResendApiKey))
            {
                services.AddResend(options =>
                {
                    options.ApiToken = emailOptions.ResendApiKey;
                });
                services.AddSingleton<IEmailService, ResendEmailService>();
                Serilog.Log.Information("Email: Resend service aktív");
            }
            else
            {
                services.AddSingleton<IEmailService, ConsoleEmailService>();
                Serilog.Log.Information("Email: Console service aktív (fejlesztői mód)");
            }
            return services;
        }

        public static IServiceCollection AddRbac(this IServiceCollection services)
        {
            //RBAC - Role Based Access Control
            services.AddHttpContextAccessor();
            services.AddScoped<IAuthorizationHandler, ProjectRoleHandler>();
            services.AddAuthorizationBuilder()
                .AddPolicy(PolicyNames.ProjectViewer, policy =>
                    policy.Requirements.Add(new ProjectRoleRequirement(ProjectRoles.Viewer)))
                .AddPolicy(PolicyNames.ProjectMember, policy =>
                    policy.Requirements.Add(new ProjectRoleRequirement(ProjectRoles.Member)))
                .AddPolicy(PolicyNames.ProjectAdmin, policy =>
                    policy.Requirements.Add(new ProjectRoleRequirement(ProjectRoles.Admin)))
                .AddPolicy(PolicyNames.ProjectOwner, policy =>
                    policy.Requirements.Add(new ProjectRoleRequirement(ProjectRoles.Owner)));
            return services;
        }

        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            //Singleton service-ek
            services.AddSingleton<ILexorankService, LexorankService>();
            services.AddSingleton<IFileStorageService, S3FileStorageService>();
            services.AddSingleton<IEncryptionService, EncryptionService>();
            //A payload parserek függőség nélküli, állapotmentes fordítók, ezért singletonok.
            //A WebhookController IEnumerable<IGitPayloadParser>-t kap,
            //és a Provider alapján választ: ez a "factory", külön regiszter-osztály nélkül.
            services.AddSingleton<IGitPayloadParser, GitHubPayloadParser>();
            services.AddSingleton<IGitPayloadParser, GitLabPayloadParser>();

            //Scoped service-ek
            services.AddScoped<IAuthService, AuthService>();
            //Az IRateLimitService NEM itt regisztrálódik: a megvalósítás a példányszámtól
            //függ, ezért az AddRateLimiting végzi.
            services.AddScoped<IProjectService, ProjectService>();
            services.AddScoped<ITaskService, TaskService>();
            services.AddScoped<ILabelService, LabelService>();
            services.AddScoped<ICommentService, CommentService>();
            services.AddScoped<IColumnService, ColumnService>();
            services.AddScoped<IBoardService, BoardService>();
            services.AddScoped<ISprintService, SprintService>();
            services.AddScoped<ITeamService, TeamService>();
            services.AddScoped<IActivityService, ActivityService>();
            services.AddScoped<IAttachmentService, AttachmentService>();
            services.AddScoped<IIntegrationService, IntegrationService>();
            services.AddScoped<IGitWebhookService, GitWebhookService>();
            services.AddScoped<IGitService, GitService>();
            services.AddScoped<IStatisticsService, StatisticsService>();
            services.AddScoped<ICounterService, CounterService>();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddScoped<ProjectNotArchivedFilter>();

            //OrphanCleanupJob - az objektumtárolóban Orphan file cleaning job
            services.AddHostedService<OrphanCleanupJob>();

            //TokenCleanupJob - lejárt hitelesítési tokenek és elévült feltöltés-naplósorok
            services.AddHostedService<TokenCleanupJob>();

            //FluentValidation Validators
            services.AddValidatorsFromAssemblyContaining<Program>();
            services.AddFluentValidationAutoValidation();

            return services;
        }
    }
}