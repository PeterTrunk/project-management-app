using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Common.Exceptions;
using ProjectManager.API.DTOs.ProjectTask;
using ProjectManager.API.Model;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.Concurrency
{
    /// <summary>
    /// A mozgatás közben indult újrasorszámozás tesztjei.
    ///
    /// <b>Miért van ez a tesztosztály.</b> A mozgatás a feladat áthelyezése ELŐTT írhat a
    /// saját sorára: ha a két szomszéd közé már nem képezhető pozíció, az oszlop teljes
    /// újrasorszámozása indul, ami a mozgatott feladatot is átírja. Ez a rendszer saját
    /// írása, nem emberi ütközés - és pontosan ebből a kettősségből jött két néma hiba:
    ///
    /// <list type="number">
    /// <item>
    /// Az újrasorszámozás újrapróbálkozó ága <c>ChangeTracker.Clear()</c>-t hívott, ami a
    /// hívó feladatát is LEVÁLASZTOTTA. Az ezt követő <c>task.Position = ...</c> egy nem
    /// követett objektumra ment, tehát az EF a mozgatást nem írta ki - a válasz mégis 200
    /// volt, és a valós idejű értesítés kiküldte a meg sem történt áthelyezést.
    /// </item>
    /// <item>
    /// A sikeres ágon a mentés visszaállította a token alapvonalát, tehát a mozgatás
    /// ütközés-ellenőrzés NÉLKÜL maradt: egy elavult tokennel érkező kérés is átment.
    /// </item>
    /// </list>
    ///
    /// <b>Amiért ez a két teszt elég.</b> A javítás egységesítette a két ágat: az
    /// újrasorszámozás mostantól MINDIG követés nélküli állapotot hagy maga után
    /// (<c>finally</c>), és a hívó minden ágon újraolvas. A két út így nem térhet el, tehát
    /// a determinisztikusan kiváltható (siker) ág mérése a versenyhelyzetes (újrapróbálás)
    /// ágra is érvényes. Magát a szerializációs ütközést külön, párhuzamos teszt méri.
    /// </summary>
    public class RebalanceDuringMoveTests(PostgresFixture fixture) : DatabaseTestBase(fixture)
    {
        /// <summary>Biztosan elavult token: a valódi érték sosem 1 egy élő soron.</summary>
        private const uint StaleToken = 1u;

        /// <summary>
        /// Olyan oszlopot állít elő, amelyben a mozgatás KÉNYTELEN újrasorszámozni: két
        /// szomszédos feladat pozíciója maximális hosszú (50 karakter) és SZOMSZÉDOS, tehát
        /// nincs közéjük beszúrható érték. A sorrendkezelő ilyenkor kivételt dob, amit a
        /// mozgatás elkap, és újrasorszámozást indít.
        ///
        /// <b>Miért a hossz kimerülésével váltjuk ki.</b> Az első változat két AZONOS
        /// pozíciót írt be, mert a kódnak van egy erre figyelő ága. Méréssel kiderült, hogy az
        /// az ág akkor NEM VOLT ELÉRHETŐ: a szomszéd lekérdezése szigorúan nagyobb pozícióra
        /// szűrt, tehát azonosat sosem adott vissza. Ez a 7. találat, és azóta javítva van
        /// (lásd <c>DuplicatePositionRepairTests</c>) - de ez a tesztosztály szándékosan a
        /// MÁSIK kiváltó utat használja, a hossz kimerülését, mert az itt vizsgált viselkedés
        /// (a változáskövetés és a konkurencia-alapvonal) független attól, mi indította az
        /// újrasorszámozást.
        ///
        /// A pozíciókat nyers SQL-lel írjuk be, mert ezt az állapotot a szolgáltatáson
        /// keresztül nem lehet előállítani - épp az a feladata, hogy ne keletkezzen.
        /// </summary>
        private async Task<(ProjectTask Prev, ProjectTask Next, ProjectTask Moved)> SeedExhaustedColumnAsync(
            SeededProject seed)
        {
            await using var ctx = CreateContext();

            var prev = await AddTaskAsync(ctx, seed, "Elozo");
            var next = await AddTaskAsync(ctx, seed, "Koveto");
            var moved = await AddTaskAsync(ctx, seed, "Mozgatott");

            //50 karakteres, szomszédos base36 értékek: ...000 és ...001 között nincs hely,
            //és 50 fölé a sorrendkezelő nem megy.
            var prevPos = "0|i" + new string('0', 48) + "0";
            var nextPos = "0|i" + new string('0', 48) + "1";

            await ctx.Database.ExecuteSqlRawAsync(
                """UPDATE "ProjectTasks" SET "Position" = {0} WHERE "Id" = {1}""", prevPos, prev.Id);
            await ctx.Database.ExecuteSqlRawAsync(
                """UPDATE "ProjectTasks" SET "Position" = {0} WHERE "Id" = {1}""", nextPos, next.Id);

            return (prev, next, moved);
        }

        private static async Task<ProjectTask> AddTaskAsync(
            Microsoft.EntityFrameworkCore.DbContext ctx, SeededProject seed, string title)
        {
            var task = new ProjectTask
            {
                ProjectId = seed.Project.Id,
                BoardId = seed.Board.Id,
                ColumnId = seed.Column.Id,
                TaskKey = $"{seed.Project.ProjKey}-{Guid.NewGuid().ToString()[..6]}",
                Title = title,
                Position = "0|i00000",
                CreatedById = seed.Owner.Id
            };

            ctx.Add(task);
            await ctx.SaveChangesAsync();
            return task;
        }

        [Fact]
        public async Task MoveTask_WhenARebalanceRuns_ActuallyPersistsTheMove()
        {
            // EZ A LÉNYEGI ÁLLÍTÁS az 1. találatra: a mozgatásnak az ADATBÁZISBAN is meg kell
            // történnie, nem csak a visszaadott objektumban. A hibás változat 200-at adott,
            // miközben a sor érintetlen maradt - ezért friss contextből olvasunk vissza.
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "RBL");
            var (prev, _, moved) = await SeedExhaustedColumnAsync(seed);

            await using var actContext = CreateContext();
            var (sut, _) = ServiceFactory.CreateTaskService(actContext, seed.Owner.Id);
            var token = await ReadRowVersionAsync(moved.Id);

            var result = await sut.MoveTaskAsync(seed.Project.Id, moved.Id, new MoveTaskDto
            {
                ColumnId = seed.Column.Id,
                AfterTaskId = prev.Id,
                RowVersion = token
            });

            await using var verify = CreateContext();
            var persisted = await verify.ProjectTasks.AsNoTracking()
                .FirstAsync(t => t.Id == moved.Id);

            // A rebalance tényleg lefutott: az új bucket 1 lett (a 0 utáni, körkörösen),
            // és a hossz visszaállt a rövid, újrakiosztott méretre.
            Assert.StartsWith("1|", persisted.Position);
            Assert.Equal(8, persisted.Position.Length);   // "1|" + 6 karakter
            // És a mozgatott feladat pozíciója a MENTETT érték, nem csak a válaszban szereplő
            Assert.Equal(result.Position, persisted.Position);
            Assert.Equal(seed.Column.Id, persisted.ColumnId);
        }

        [Fact]
        public async Task MoveTask_WithAStaleToken_ConflictsEvenWhenARebalanceWouldRun()
        {
            // EZ A LÉNYEGI ÁLLÍTÁS a 3. találatra: az újrasorszámozás a RENDSZER írása, és
            // nem veheti át egy elavult kliens-token helyét. A hibás változat itt 200-at
            // adott, mert a rebalance mentése felülírta a token alapvonalát - vagyis a
            // mozgatás ütközés-ellenőrzés nélkül futott le.
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "RBS");
            var (prev, _, moved) = await SeedExhaustedColumnAsync(seed);

            await using var actContext = CreateContext();
            var (sut, _) = ServiceFactory.CreateTaskService(actContext, seed.Owner.Id);

            await Assert.ThrowsAsync<ConflictException>(
                () => sut.MoveTaskAsync(seed.Project.Id, moved.Id, new MoveTaskDto
                {
                    ColumnId = seed.Column.Id,
                    AfterTaskId = prev.Id,
                    RowVersion = StaleToken
                }));

            // És a visszautasítás előtt NEM nyúlt hozzá a többi sorhoz: az ellenőrzés az
            // újrasorszámozás ELŐTT fut, különben előbb írnánk, mint hogy megkérdeznénk.
            await using var verify = CreateContext();
            var untouched = await verify.ProjectTasks.AsNoTracking()
                .FirstAsync(t => t.Id == prev.Id);

            Assert.StartsWith("0|", untouched.Position);
            Assert.Equal(52, untouched.Position.Length);   // "0|" + 50 karakter: az eredeti
        }

        private async Task<uint> ReadRowVersionAsync(Guid taskId)
        {
            await using var ctx = CreateContext();
            var task = await ctx.ProjectTasks.AsNoTracking().FirstAsync(t => t.Id == taskId);
            return task.xmin;
        }
    }
}
