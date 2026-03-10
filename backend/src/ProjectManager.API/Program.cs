using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ProjectManager.API.Authorization.Handlers;
using ProjectManager.API.Authorization.Requirements;
using ProjectManager.API.Data;
using ProjectManager.API.Services.Auth;
using ProjectManager.API.Services.CommentService;
using ProjectManager.API.Services.LabelService;
using ProjectManager.API.Services.ProjectService;
using ProjectManager.API.Services.ProjectTaskService;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

//Service Registration (DI Container)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
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
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });
builder.Services.AddSwaggerGen(options =>
{
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

//FliendValidation Validators
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<ILabelService, LabelService>();
builder.Services.AddScoped<ICommentService, CommentService>();

var app = builder.Build(); // Határ: konfiguráció fent, pipeline lent

//Middleware Pipeline - Sorrendjük kritikus
if (app.Environment.IsDevelopment())
{
    //Middleware hozzáadás ami development specifikus
    app.UseSwagger();
    app.UseSwaggerUI();

    //DbSeeding és Migráció, nem middleware de az inditáskor lefutnak egyszer!
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
}

//Middleware hozzáadás
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

//Start
app.Run();

