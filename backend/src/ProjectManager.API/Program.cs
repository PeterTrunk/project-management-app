using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ProjectManager.API.Authorization.Handlers;
using ProjectManager.API.Authorization.Requirements;
using ProjectManager.API.Data;
using ProjectManager.API.Filters;
using ProjectManager.API.Hubs;
using ProjectManager.API.Services.ActivityService;
using ProjectManager.API.Services.AttachmentService;
using ProjectManager.API.Services.Auth;
using ProjectManager.API.Services.BoardService;
using ProjectManager.API.Services.ColumnService;
using ProjectManager.API.Services.CommentService;
using ProjectManager.API.Services.CurrentUserService;
using ProjectManager.API.Services.FileStorageService;
using ProjectManager.API.Services.LabelService;
using ProjectManager.API.Services.LexorankService;
using ProjectManager.API.Services.ProjectService;
using ProjectManager.API.Services.ProjectTaskService;
using ProjectManager.API.Services.SprintService;
using ProjectManager.API.Services.TeamService;
using System.Reflection;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// .env fájl betöltése
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
        {
            Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
        }
    }
}

builder.Configuration.AddEnvironmentVariables();

// Environment variables kinyerése
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? throw new InvalidOperationException("JWT_SECRET nincs beállítva!");

var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER")
    ?? throw new InvalidOperationException("JWT_ISSUER nincs beállítva!");

var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE")
    ?? throw new InvalidOperationException("JWT_AUDIENCE nincs beállítva!");

var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? throw new InvalidOperationException("DATABASE_URL nincs beállítva!");

// Service Registration (DI Container)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecret))
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

builder.Services.AddSignalR();

builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    //options.IncludeXmlComments(xmlPath);
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

var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL")
    ?? throw new InvalidOperationException("FRONTEND_URL nincs beállítva!");

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

//RBAC - Role Based Access Control
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuthorizationHandler, ProjectRoleHandler>();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("ProjectViewer", policy =>
        policy.Requirements.Add(new ProjectRoleRequirement("Viewer")))
    .AddPolicy("ProjectMember", policy =>
        policy.Requirements.Add(new ProjectRoleRequirement("Member")))
    .AddPolicy("ProjectAdmin", policy =>
        policy.Requirements.Add(new ProjectRoleRequirement("Admin")))
    .AddPolicy("ProjectOwner", policy =>
        policy.Requirements.Add(new ProjectRoleRequirement("Owner")));

//FluentValidation Validators
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddSingleton<ILexorankService, LexorankService>();
builder.Services.AddSingleton<IFileStorageService, MinIOFileStorageService>();

builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<ILabelService, LabelService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<IColumnService, ColumnService>();
builder.Services.AddScoped<IBoardService, BoardService>();
builder.Services.AddScoped<ISprintService, SprintService>();
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IActivityService, ActivityService>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddScoped<ProjectNotArchivedFilter>();

var app = builder.Build(); // Határ: konfiguráció fent, pipeline lent

//Middleware Pipeline - Sorrendjük kritikus
if (app.Environment.IsDevelopment())
{
    //Middleware hozzáadás ami development specifikus
    app.UseSwagger();
    app.UseSwaggerUI();

    //Migráció, nem middleware de az inditáskor lefutnak egyszer!
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

//Middleware hozzáadás
app.UseRouting();

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHub<ProjectHub>("/hubs/project");

//Start
app.Run();

