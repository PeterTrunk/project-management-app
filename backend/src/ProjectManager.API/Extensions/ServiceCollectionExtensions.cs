using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
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
        public static IServiceCollection AddDatabase(this IServiceCollection services, string connectionString)
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(connectionString));
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

        public static IServiceCollection AddRedisAndSignalR(this IServiceCollection services, string? redisConnection)
        {
            var signalRBuilder = services.AddSignalR();
            if (!string.IsNullOrEmpty(redisConnection))
            {
                //SignalR backplane
                signalRBuilder.AddStackExchangeRedis(redisConnection, options =>
                {
                    options.Configuration.ChannelPrefix = RedisChannel.Literal("ProjectManager");
                });

                //Rate limiting-hez külön regisztráció multiplexerként
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
            services.AddSingleton<IFileStorageService, MinIOFileStorageService>();
            services.AddSingleton<IEncryptionService, EncryptionService>();

            //Scoped service-ek
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IRateLimitService, RateLimitService>();
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

            //OrphanCleanupJob - MiniO filestorage Orphan file cleaning job
            services.AddHostedService<OrphanCleanupJob>();

            //FluentValidation Validators
            services.AddValidatorsFromAssemblyContaining<Program>();
            services.AddFluentValidationAutoValidation();

            return services;
        }
    }
}