using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.Data;
using ProjectManager.API.Hubs;
using ProjectManager.API.Middleware;
using ProjectManager.API.Model;
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

        /// <summary>
        /// Gondoskodik arról, hogy a kódban rögzített hatályos dokumentumverzió szerepeljen az adatbázisban.
        /// Enélkül a regisztráció elutasítana minden kérést, mert nem lenne mire hivatkoznia az elfogadásnak.
        ///
        /// Szándékosan nem migrációs InsertData: a verzió a kódhoz tartozik, és minden indulásnál ellenőrizhető
        /// Így egy visszaállított vagy kézzel ürített adatbázisban is helyreáll.
        /// </summary>
        public static async Task SeedTermsVersionAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var version = LegalDocuments.CurrentVersion;

            if (await context.TermsVersions.AnyAsync(tv => tv.Version == version))
            {
                Serilog.Log.Information("Hatályos dokumentumverzió: {Version}", version);
                return;
            }

            context.TermsVersions.Add(new TermsVersion
            {
                Version = version,
                EffectiveFrom = LegalDocuments.CurrentVersionEffectiveFrom
            });

            try
            {
                await context.SaveChangesAsync();
                Serilog.Log.Information("Új dokumentumverzió rögzítve: {Version}", version);
            }
            catch (DbUpdateException ex)
            {
                //Több replika egyszerre indulhat: a Version egyedi indexe dönti el, ki nyer.
                //A vesztes ága nem hiba, a sor létezik - ez a lényeg.
                Serilog.Log.Information(ex,
                    "A dokumentumverziót közben egy másik példány rögzítette: {Version}", version);
                context.ChangeTracker.Clear();
            }
        }

        public static async Task MigrateWebhookSecretsAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var encryption = scope.ServiceProvider.GetRequiredService<IEncryptionService>();

            //Csak a jelöletlen sorok érdekelnek. A már megjelölt (enc:v1:) értékekhez hozzá sem nyúlunk
            //Így egy kulcscsere nem tud kárt tenni bennük.
            var pending = await context.Integrations
                .Where(i => !i.WebhookSecret.StartsWith(EncryptionService.Prefix))
                .ToListAsync();

            if (pending.Count == 0)
                return;

            Serilog.Log.Information("WebhookSecret migráció indul | Érintett sorok: {Count}", pending.Count);

            foreach (var integration in pending)
            {
                //A jelöletlen érték kétféle lehet: plain text, vagy a jelölés bevezetése előtti ciphertext.
                //A kettőt a Decrypt sikeressége különbözteti meg:
                //DE ha a Decrypt hibázik, az rossz ENCRYPTION_KEY-t is jelenthet.
                //Ilyenkor NEM titkosítunk újra, mert azzal helyrehozhatatlanul
                //felülírnánk az eredeti secretet: naplózunk és kihagyjuk a sort.
                string migrated;

                if (LooksLikeCiphertext(integration.WebhookSecret))
                {
                    try
                    {
                        var plaintext = encryption.Decrypt(integration.WebhookSecret);
                        migrated = encryption.Encrypt(plaintext);
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex,
                            "WebhookSecret nem fejthető vissza - a sor változatlan marad. " +
                            "Ellenőrizd az ENCRYPTION_KEY-t! | IntegrationId: {IntegrationId}",
                            integration.Id);
                        continue;
                    }
                }
                else
                {
                    migrated = encryption.Encrypt(integration.WebhookSecret);
                }

                integration.WebhookSecret = migrated;

                try
                {
                    //Soronkénti mentés: egy hibás sor ne vigye magával a többit
                    await context.SaveChangesAsync();
                    Serilog.Log.Information("Integration WebhookSecret migrálva | IntegrationId: {IntegrationId}", integration.Id);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "WebhookSecret mentési hiba | IntegrationId: {IntegrationId}", integration.Id);
                    context.ChangeTracker.Clear();
                }
            }

            Serilog.Log.Information("WebhookSecret migráció befejezve");
        }

        // A titkosítás bevezetése előtt mentett TOTP titkok titkosítása:
        // 
        // A WebhookSecret migrációjánál egyszerűbb az eset:
        // a TotpSecret sosem volt titkosítva, tehát minden jelöletlen érték biztosan nyers base32 - nincs mit megkülönböztetni,
        // és nincs az a kockázat, hogy egy helyesen titkosított értéket írnánk felül.
        //
        // Idempotens: a jelölt (enc:v1:) sorokhoz hozzá sem nyúl, így minden indulásnál lefuthat.
        // Egy visszaállított, régi mentésből érkező sort is rendbe tesz.
        public static async Task EncryptExistingTotpSecretsAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var encryption = scope.ServiceProvider.GetRequiredService<IEncryptionService>();

            var pending = await context.Users
                .Where(u => u.TotpSecret != null && !u.TotpSecret.StartsWith(EncryptionService.Prefix))
                .ToListAsync();

            if (pending.Count == 0)
                return;

            Serilog.Log.Information("TotpSecret titkosítás indul | Érintett sorok: {Count}", pending.Count);

            foreach (var user in pending)
            {
                user.TotpSecret = encryption.Encrypt(user.TotpSecret!);

                try
                {
                    //Soronkénti mentés: egy hibás sor ne vigye magával a többit
                    await context.SaveChangesAsync();
                    Serilog.Log.Information("TotpSecret titkosítva | UserId: {UserId}", user.Id);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "TotpSecret mentési hiba | UserId: {UserId}", user.Id);
                    context.ChangeTracker.Clear();
                }
            }

            Serilog.Log.Information("TotpSecret titkosítás befejezve");
        }

        //A mi ciphertextünk base64, és dekódolva legalább nonce + tag hosszú.
        //Ennél rövidebb vagy nem base64 érték biztosan plain text.
        private static bool LooksLikeCiphertext(string value)
        {
            var buffer = new byte[value.Length];
            return Convert.TryFromBase64String(value, buffer, out var written) && written >= 12 + 16;
        }
    }
}