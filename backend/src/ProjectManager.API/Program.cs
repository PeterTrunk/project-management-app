using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProjectManager.API.Data;
using ProjectManager.API.Services.Auth;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

//Service Registration (DI Container)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
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

