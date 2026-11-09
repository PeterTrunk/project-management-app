using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Common.Exceptions;
using ProjectManager.API.DTOs.Attachment;
using ProjectManager.API.Model;
using ProjectManager.API.Services.AttachmentService;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.Storage
{
    /// <summary>
    /// A feltöltés megerősítésének tesztjei: a naplósor igénylése és annak visszavonása.
    /// </summary>
    /// <remarks>
    /// <b>Miért van ez a tesztosztály.</b> A megerősítés két lépésből áll - a naplósort
    /// megjelöli megerősítettként, és létrehozza a csatolmány-rekordot -, és a kettő között
    /// hibázhat. Két néma hiba volt itt:
    ///
    /// <list type="number">
    /// <item>
    /// A jelölés a <c>SaveChanges</c>-re várt, a hibaágon pedig a
    /// <c>ChangeTracker.Clear()</c> UTÁN kapott értéket - vagyis egy leválasztott entitáson,
    /// tehát sosem jutott el az adatbázisba. A takarító feladat a meg nem erősített
    /// naplósorokat a hozzájuk tartozó FÁJLLAL együtt viszi el: egy élő csatolmány fájlja
    /// tűnt volna el a tárolóból, miközben az adatbázis szerint minden rendben van.
    /// </item>
    /// <item>
    /// A duplikált megerősítés ellenőrzése olvasás és írás között állt (TOCTOU), tehát két
    /// egyidejű hívás mindkettő szabadnak látta a naplósort. Az invariánst csak az
    /// <c>Attachments.StorageKey</c> egyedi indexe tartotta - félrevezető hibaüzenettel.
    /// </item>
    /// </list>
    ///
    /// A tároló kézi dupla, mert a vizsgált viselkedés az ADATBÁZISBAN van. A tároló
    /// tényleges kikényszerítését (aláírt típus és méret) a
    /// <c>PresignedUploadPolicyTests</c> méri, saját konténerrel.
    /// </remarks>
    public class ConfirmUploadClaimTests(PostgresFixture fixture) : DatabaseTestBase(fixture)
    {
        private const string FileName = "terv.png";
        private const string ContentType = "image/png";
        private const long SizeBytes = 1024;

        /// <summary>
        /// Végigvezeti a feltöltést az URL igényléséig, és a "tárolóba" is beteszi az
        /// objektumot - vagyis abba az állapotba, amelyben a kliens a megerősítést hívja.
        /// </summary>
        private async Task<(string StorageKey, Guid LogId)> ArrangePendingUploadAsync(
            AttachmentService sut, FakeFileStorageService storage, SeededProject seed)
        {
            var presigned = await sut.GetPresignedUploadUrlAsync(seed.Project.Id, null,
                new PresignedUrlRequestDto
                {
                    FileName = FileName,
                    ContentType = ContentType,
                    SizeBytes = SizeBytes
                });

            storage.PutObject(presigned.StorageKey, ContentType, SizeBytes);

            await using var ctx = CreateContext();
            var log = await ctx.PresignedUrlLogs.AsNoTracking()
                .FirstAsync(p => p.StorageKey == presigned.StorageKey);

            return (presigned.StorageKey, log.Id);
        }

        [Fact]
        public async Task ConfirmUpload_ASikeresMegerositest_AzAdatbazisbaIsKiirja()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "CFA");
            var (sut, storage, _) = ServiceFactory.CreateAttachmentService(context, seed.Owner.Id);
            var (storageKey, logId) = await ArrangePendingUploadAsync(sut, storage, seed);

            var result = await sut.ConfirmUploadAsync(seed.Project.Id, null,
                new ConfirmUploadDto { StorageKey = storageKey });

            await using var verify = CreateContext();
            var log = await verify.PresignedUrlLogs.AsNoTracking().FirstAsync(p => p.Id == logId);

            Assert.True(log.Confirmed);
            Assert.Equal(FileName, result.FileName);
            //A navigáció szándékosan nincs bekötve, a nevet külön adjuk át - ha ez elcsúszik,
            //a válaszban "Ismeretlen" állna.
            Assert.Equal(seed.Owner.DisplayName, result.UploadedByName);
        }

        [Fact]
        public async Task ConfirmUpload_EgyidejuVerzioutkozesUtan_IsKiirjaAMegerositest()
        {
            // EZ A LÉNYEGI ÁLLÍTÁS az 1. hibára, és SZÁNDÉKOSAN párhuzamos.
            //
            // Az első változatom determinisztikusan akarta kiváltani az újrapróbálkozást: egy
            // blokkoló csatolmánnyal, ami az első verziószámot elfoglalja. Méréssel kiderült,
            // hogy ez NEM MŰKÖDIK, és az ok tanulságos: a szolgáltatás a verziót a meglévő
            // darabszámból képzi, tehát egy foglalt sor a darabszámot is eggyel növeli -
            // a kísérlet mindig a következő SZABAD verzióra esik. A ciklus egy változatlan
            // adathalmazon sosem tud másodszor mást próbálni.
            //
            // Vagyis az újrapróbálkozó ág KIZÁRÓLAG versenyhelyzetben érhető el: akkor, ha
            // egy másik kérés a két kísérlet KÖZÖTT commitál. Ezért két külön kapcsolaton
            // futó megerősítés indul itt, ugyanazzal a fájlnévvel, külön tárolókulccsal.
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "CFR");

            await using var ctxA = CreateContext();
            await using var ctxB = CreateContext();
            var (sutA, storageA, _) = ServiceFactory.CreateAttachmentService(ctxA, seed.Owner.Id);
            var (sutB, storageB, _) = ServiceFactory.CreateAttachmentService(ctxB, seed.Owner.Id);

            var (keyA, logA) = await ArrangePendingUploadAsync(sutA, storageA, seed);
            var (keyB, logB) = await ArrangePendingUploadAsync(sutB, storageB, seed);

            var results = await Task.WhenAll(
                sutA.ConfirmUploadAsync(seed.Project.Id, null, new ConfirmUploadDto { StorageKey = keyA }),
                sutB.ConfirmUploadAsync(seed.Project.Id, null, new ConfirmUploadDto { StorageKey = keyB }));

            await using var verify = CreateContext();
            var logs = await verify.PresignedUrlLogs.AsNoTracking()
                .Where(p => p.Id == logA || p.Id == logB)
                .ToListAsync();

            // MINDKETTŐ megerősítése kiíródott. Ha a verseny újrapróbálkozást váltott ki, a
            // hibás változatban a vesztes naplósora `false` maradt - és a takarító feladat
            // 15 perccel később elvitte volna egy ÉLŐ csatolmány fájlját a tárolóból.
            Assert.All(logs, log => Assert.True(log.Confirmed));
            Assert.Equal(2, logs.Count);

            // És a két csatolmány külön verziószámot kapott: az egyedi index tartott.
            Assert.Equal(new[] { 0, 1 }, results.Select(r => r.Version).OrderBy(v => v));
        }

        [Fact]
        public async Task ConfirmUpload_UgyanarraAKulcsraEgyidejuleg_PontosanEgyszerSikerul()
        {
            // EZ A LÉNYEGI ÁLLÍTÁS a 2. hibára (TOCTOU). A korábbi `if (log.Confirmed)`
            // ellenőrzés olvasás és írás között állt, tehát két egyidejű hívás mindkettő
            // szabadnak látta a naplósort. Az invariánst csak az `Attachments.StorageKey`
            // egyedi indexe tartotta - a vesztes ezért "Nem sikerült menteni a fájlt"
            // üzenetet kapott három hiábavaló újrapróbálkozás után, nem azt, hogy a fájl már
            // meg van erősítve.
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "CFD");

            await using var ctxA = CreateContext();
            await using var ctxB = CreateContext();
            var (sutA, storageA, _) = ServiceFactory.CreateAttachmentService(ctxA, seed.Owner.Id);
            var (sutB, storageB, _) = ServiceFactory.CreateAttachmentService(ctxB, seed.Owner.Id);

            var (storageKey, _) = await ArrangePendingUploadAsync(sutA, storageA, seed);
            //A másik hívó UGYANARRA a kulcsra érkezik - a tárolóban is ott az objektum.
            storageB.PutObject(storageKey, ContentType, SizeBytes);

            var outcomes = await Task.WhenAll(
                RunAsync(() => sutA.ConfirmUploadAsync(seed.Project.Id, null,
                    new ConfirmUploadDto { StorageKey = storageKey })),
                RunAsync(() => sutB.ConfirmUploadAsync(seed.Project.Id, null,
                    new ConfirmUploadDto { StorageKey = storageKey })));

            // Pontosan egy siker, és a vesztes ÜTKÖZÉST jelez - nem mentési hibát.
            Assert.Single(outcomes, o => o == null);
            var failure = Assert.Single(outcomes, o => o != null);
            Assert.IsType<ConflictException>(failure);

            // Egy fájl, egy csatolmány.
            await using var verify = CreateContext();
            var count = await verify.Attachments.CountAsync(a => a.StorageKey == storageKey);

            Assert.Equal(1, count);
        }

        /// <summary>Lefuttatja a műveletet, és a kivételt adja vissza hiba helyett.</summary>
        private static async Task<Exception?> RunAsync(Func<Task> action)
        {
            try
            {
                await action();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        [Fact]
        public async Task ConfirmUpload_HaACsatolmanyNemJonLetre_VisszavonjaAzIgenylest()
        {
            // EZ A LÉNYEGI ÁLLÍTÁS a visszavonásra. Ha a megerősítés ténye megmaradna egy
            // csatolmány nélküli naplósoron, akkor a takarító nem vinné el a fájlt
            // (megerősítettnek látja), a felhasználó viszont újra sem tudná megerősíteni -
            // a fájl örökre a tárolóban ragadna.
            //
            // A hibát úgy kényszerítjük ki, hogy a verziószám MINDEN kísérletnél ütközzön.
            //
            // A szolgáltatás a verziót a meglévő darabszámból számolja, és minden kísérletnél
            // ÚJRASZÁMOLJA - a bukott kísérlet viszont nem hoz létre sort, tehát a darabszám
            // nem változik. Ha a már foglalt verziók 1 és 2, akkor a darabszám 2, a kísérlet
            // pedig mindig a (már foglalt) 2-es verzióval megy: mindhárom próba elbukik.
            //
            // A 0-s verziót szándékosan kihagyjuk - ez a kulcs. Ha 0-tól folytonosan
            // foglalnánk, a darabszám épp a következő SZABAD verzióra mutatna, és az első
            // kísérlet sikerülne.
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "CFV");
            var (sut, storage, _) = ServiceFactory.CreateAttachmentService(context, seed.Owner.Id);
            var (storageKey, logId) = await ArrangePendingUploadAsync(sut, storage, seed);

            await using var blocker = CreateContext();
            foreach (var version in new[] { 1, 2 })
            {
                blocker.Attachments.Add(new Attachment
                {
                    Id = Guid.NewGuid(),
                    ProjectId = seed.Project.Id,
                    UploadedById = seed.Owner.Id,
                    FileName = FileName,
                    ContentType = ContentType,
                    SizeBytes = SizeBytes,
                    StorageKey = $"masik/kulcs/{version}/terv.png",
                    AttachmentType = "Image",
                    Version = version,
                    CreatedAt = DateTime.UtcNow
                });
            }
            await blocker.SaveChangesAsync();

            await Assert.ThrowsAnyAsync<AppException>(
                () => sut.ConfirmUploadAsync(seed.Project.Id, null,
                    new ConfirmUploadDto { StorageKey = storageKey }));

            await using var verify = CreateContext();
            var log = await verify.PresignedUrlLogs.AsNoTracking().FirstAsync(p => p.Id == logId);

            // Az igénylés visszavonva: a takarító elviheti a fájlt, és az újrapróbálkozás járható.
            Assert.False(log.Confirmed);
        }
    }
}
