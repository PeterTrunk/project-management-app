using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Data;
using ProjectManager.API.Hubs;
using ProjectManager.API.Middleware;
using ProjectManager.API.Services.EncryptionService;

namespace ProjectManager.API.Extensions
{
    public static class ApplicationBuilderExtensions
    {
        public static WebApplication UseProjectManagerMiddleware(this WebApplication app)
        {
            //Legelső dolog, hogy a nem várt hibákat már most ezzel kezelje!
            app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseRouting();
            app.UseCors("AllowFrontend");
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();
            app.MapHub<ProjectHub>("/hubs/project");
            app.MapGet("/health", () => "OK");

            return app;
        }

        public static async Task RunMigrationsAsync(this WebApplication app)
        {
            // Retry logika a migrációhoz
            var retries = 10;
            while (retries > 0)
            {
                try
                {
                    using var scope = app.Services.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    await db.Database.MigrateAsync();
                    Serilog.Log.Information("Adatbázis migráció sikeres!");
                    break;
                }
                catch (Exception ex)
                {
                    retries--;
                    Serilog.Log.Warning("Migration failed, retrying... ({Retries} attempts left): {Message}", retries, ex.Message);
                    if (retries == 0) throw;
                    await Task.Delay(3000);
                }
            }
        }

        public static async Task MigrateWebhookSecretsAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var encryption = scope.ServiceProvider.GetRequiredService<IEncryptionService>();

            var integrations = await context.Integrations.ToListAsync();
            foreach (var integration in integrations)
            {
                try
                {
                    ///Ha már titkosított Decrypt sikeres lesz, kihagyjuk
                    encryption.Decrypt(integration.WebhookSecret);
                }
                catch
                {
                    //Ha Decrypt hibát dob,akkor még plain text, titkosítjuk
                    integration.WebhookSecret = encryption.Encrypt(integration.WebhookSecret);
                    Serilog.Log.Information("Integration WebhookSecret titkosítva | IntegrationId: {IntegrationId}", integration.Id);
                }
            }

            await context.SaveChangesAsync();
            Serilog.Log.Information("WebhookSecret migráció befejezve");
        }
    }
}