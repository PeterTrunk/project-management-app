using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.Common.Exceptions;
using ProjectManager.API.DTOs.Columns;
using ProjectManager.API.DTOs.ProjectTask;
using ProjectManager.API.Model;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.Concurrency
{
    /// <summary>
    /// VALÓDI párhuzamossággal futó írások tesztjei.
    /// </summary>
    /// <remarks>
    /// <b>Miért külön osztály, és miért ez a legfontosabb a konkurencia-tesztek közül.</b>
    /// A <c>ConcurrencyTokenRoundTripTests</c> és a
    /// <c>StaleTokenOnNoOpUpdateTests</c> szekvenciális: elavult tokent SZIMULÁLNAK. Az
    /// itt vizsgált három helyzet viszont csak akkor áll elő, ha két kérés tényleg
    /// egyszerre fut - és a felülvizsgálat előtt a rendszerben pontosan EGYETLEN ilyen
    /// mérés volt (a feladatazonosító-számlálón).
    ///
    /// Minden hívó <b>saját contextet, tehát saját adatbázis-kapcsolatot</b> kap. Enélkül a
    /// párhuzamosság látszólagos lenne: ugyanazon a kapcsolaton a műveletek sorban futnának,
    /// és a teszt nem mérne semmit.
    ///
    /// <b>Amit ezek a tesztek állítanak, és amit nem.</b> Egy versenyhelyzet kimenete nem
    /// determinisztikus - a tesztek ezért nem azt rögzítik, hogy melyik hívó nyer, hanem az
    /// INVARIÁNST, ami minden kimenetben igaz. Ahol a rendszer tudatosan vállal
    /// pontatlanságot (sorrend-ütközés), ott azt rögzítik, hogy <b>pontosan mit</b> vállal -
    /// mert a „vállalt kompromisszum" csak akkor döntés, ha meg is van mérve.
    /// </remarks>
    public class ParallelWriteTests(PostgresFixture fixture) : DatabaseTestBase(fixture)
    {
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

        // ---------------------------------------------------------------------------------
        // 1. forgatókönyv: két egyidejű sprint-aktiválás
        // ---------------------------------------------------------------------------------

        [Fact]
        public async Task ActivateSprint_KetEgyidejuAktivalas_PontosanEgyNyer()
        {
            // Ez az invariáns HÁROM védelmi szinten áll (3.4.2.): a validátor az
            // értékhalmazt, az alkalmazás-logika a tranzakción belüli ellenőrzést, az
            // adatbázis a szűrt egyedi indexet adja. A szekvenciális teszt a második szintet
            // méri; EZ a teszt az elsőt és a harmadikat is.
            //
            // A PostgreSQL Serializable szintje itt nem írás-írás ütközést lát (a két kérés
            // KÜLÖN sort ír), hanem write skew-t: mindkettő azt olvasta, hogy nincs aktív
            // sprint, és mindkettő olyat ír, amitől ez már nem igaz. Ezt a szerver
            // predikátum-zárolással észleli, és az egyik tranzakciót megszakítja.
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "PAS");

            //A seed egy Planning sprintet ad; kell egy második is.
            await using var arrange = CreateContext();
            var second = new Sprint
            {
                ProjectId = seed.Project.Id,
                Name = "PAS Sprint 2",
                State = SprintStates.Planning
            };
            arrange.Sprints.Add(second);
            await arrange.SaveChangesAsync();

            await using var ctxA = CreateContext();
            await using var ctxB = CreateContext();
            var (sutA, _) = ServiceFactory.CreateSprintService(ctxA, seed.Owner.Id);
            var (sutB, _) = ServiceFactory.CreateSprintService(ctxB, seed.Owner.Id);

            var outcomes = await Task.WhenAll(
                RunAsync(() => sutA.ActivateSprintAsync(seed.Project.Id, seed.Sprint.Id)),
                RunAsync(() => sutB.ActivateSprintAsync(seed.Project.Id, second.Id)));

            //A LÉNYEGI ÁLLÍTÁS: pontosan egy aktív sprint van az adatbázisban.
            await using var verify = CreateContext();
            var activeCount = await verify.Sprints
                .CountAsync(s => s.ProjectId == seed.Project.Id && s.State == SprintStates.Active);

            Assert.Equal(1, activeCount);

            //És a vesztes KEZELT hibát kap, nem belső szerverhibát. Ez a `ConcurrencyErrors`
            //munkája: a szerializációs hiba és az egyediség-sértés is 409-re képződik.
            var failures = outcomes.Where(o => o != null).ToList();
            Assert.Single(failures);
            Assert.IsAssignableFrom<AppException>(failures[0]);
        }

        // ---------------------------------------------------------------------------------
        // 4. forgatókönyv: két egyidejű mozgatás ugyanabba a résbe
        // ---------------------------------------------------------------------------------

        [Fact]
        public async Task MoveTask_KetEgyidejuMozgatasUgyanabbaAResbe_SemmitNemVesztunkEl()
        {
            // EZT A HELYZETET A RENDSZER TUDATOSAN NEM VÉDI (3.8.7.), és ez a teszt azt
            // rögzíti, hogy PONTOSAN MIT vállalunk vele.
            //
            // Mindkét hívó ugyanazt a két szomszédot olvassa, tehát ugyanazt a közbenső
            // pozíciót számolja. A két írás KÜLÖN sorra megy, mindkettő tokenje érvényes,
            // tehát a konkurencia-token egyiket sem fogja meg.
            //
            // MÉRVE, öt körből ötször ugyanez: mindkét mozgatás SIKERÜL, és a két feladat
            // AZONOS pozíciót kap (a mérésben `0|f`). Vagyis amit vállalunk, az nem
            // elméleti pontatlanság, hanem konkrétan ez: két feladat azonos sorrendkulccsal,
            // amelyek egymáshoz képesti megjelenítési sorrendje nem meghatározott.
            //
            // A duplikációt a rendszer NEM ELŐZI MEG - a számítás pillanatában egyik kérés
            // sem lát még ikerpárt. Amit megad: a következő, a duplikált kulcsra horgonyzó
            // beszúrás felismeri és újrasorszámozással kitakarítja az oszlopot. Ennek a
            // felismerésnek a tesztje a `DuplicatePositionRepairTests`; ez a teszt csak a
            // keletkezését és annak korlátait rögzíti.
            //
            // (A javítás a 7. találat nyomán készült: az a védelmi ág korábban megvolt, de a
            // szomszéd-lekérdezés szigorú összehasonlítása miatt soha nem futott le.)
            //
            // Amit NEM vállalunk, és amit ez a teszt állít: egyetlen mozgatás sem tűnik el
            // némán, egyetlen feladat sem kerül rossz oszlopba, és a pozíciók a kijelölt
            // résen BELÜL maradnak - tehát a két feladat a szomszédaihoz képest a helyén van.
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "PMV");

            await using var arrange = CreateContext();
            var prev = AddTask(arrange, seed, "Elozo", "0|c");
            var next = AddTask(arrange, seed, "Koveto", "0|y");
            var moveA = AddTask(arrange, seed, "Mozgatott A", "0|z1");
            var moveB = AddTask(arrange, seed, "Mozgatott B", "0|z2");
            await arrange.SaveChangesAsync();

            await using var ctxA = CreateContext();
            await using var ctxB = CreateContext();
            var (sutA, _) = ServiceFactory.CreateTaskService(ctxA, seed.Owner.Id);
            var (sutB, _) = ServiceFactory.CreateTaskService(ctxB, seed.Owner.Id);

            var tokenA = await ReadRowVersionAsync(moveA.Id);
            var tokenB = await ReadRowVersionAsync(moveB.Id);

            var outcomes = await Task.WhenAll(
                RunAsync(() => sutA.MoveTaskAsync(seed.Project.Id, moveA.Id, new MoveTaskDto
                {
                    ColumnId = seed.Column.Id, AfterTaskId = prev.Id, RowVersion = tokenA
                })),
                RunAsync(() => sutB.MoveTaskAsync(seed.Project.Id, moveB.Id, new MoveTaskDto
                {
                    ColumnId = seed.Column.Id, AfterTaskId = prev.Id, RowVersion = tokenB
                })));

            //Ami sikerült, az az adatbázisban is megtörtént; ami nem, az KEZELT hibát adott.
            foreach (var failure in outcomes.Where(o => o != null))
                Assert.IsAssignableFrom<AppException>(failure!);

            await using var verify = CreateContext();
            var tasks = await verify.ProjectTasks.AsNoTracking()
                .Where(t => t.ProjectId == seed.Project.Id)
                .ToListAsync();

            //Egyetlen feladat sem veszett el, és egyik sem került idegen oszlopba.
            Assert.Equal(5, tasks.Count);   // a seed egy taskja + a négy felvett
            Assert.All(tasks, t => Assert.Equal(seed.Column.Id, t.ColumnId));

            //A pozíciók a két szomszéd KÖZÉ esnek - a sorrend akkor is értelmes marad, ha a
            //két mozgatott feladat egymáshoz képest nem meghatározott. Csak az ELMOZDULT
            //feladatokra állítjuk: egy elbukott mozgatás a régi pozícióján maradna, és az a
            //résen kívül van.
            var moved = tasks
                .Where(t => t.Id == moveA.Id || t.Id == moveB.Id)
                .Where(t => t.Position != "0|z1" && t.Position != "0|z2")
                .ToList();

            Assert.NotEmpty(moved);

            foreach (var t in moved)
            {
                Assert.True(string.CompareOrdinal(t.Position, "0|c") > 0, $"nem prev utan: {t.Position}");
                Assert.True(string.CompareOrdinal(t.Position, "0|y") < 0, $"nem next elott: {t.Position}");
            }
        }

        [Fact]
        public async Task MoveTask_KetEgyidejuMozgatasAzOSZLOPELEJERE_UgyanazAzEredmeny()
        {
            // Ugyanaz a hibaosztály, HORGONY NÉLKÜL. Érdemes külön mérni, mert a kód ezt az
            // esetet más ágon kezeli: horgony hiányában nincs mihez hasonlítani, ezért a
            // szomszéd az oszlop első feladata, a pozíció pedig `GetBefore`-ral képződik -
            // nem `GetBetween`-nel.
            //
            // A várakozás az volt, hogy ugyanaz történik, mint horgonnyal: mindkét hívó
            // ugyanazt az első feladatot látja, ugyanazt az "elé" értéket számolja, tehát a
            // két mozgatott feladat azonos kulcsot kap.
            //
            // MÉRVE, öt körből ötször: mindkét mozgatás SIKERÜL, és mindkettő ugyanazt a
            // pozíciót kapja (a mérésben `0|9`). Vagyis a vállalt pontatlanság nem a horgony
            // KEZELÉSÉBŐL jön, hanem abból, hogy a pozíciószámítás a beolvasott állapotra
            // épül, és az két kérés között elavul - a horgony megléte vagy hiánya ezen nem
            // változtat. Ezt a duplikációt a következő, az oszlop elejére irányuló beszúrás
            // ismeri fel és javítja (`DuplicatePositionRepairTests`).
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "PFR");

            await using var arrange = CreateContext();
            var anchor = AddTask(arrange, seed, "Legelso", "0|m00000");
            var moveA = AddTask(arrange, seed, "Mozgatott A", "0|z00001");
            var moveB = AddTask(arrange, seed, "Mozgatott B", "0|z00002");
            await arrange.SaveChangesAsync();

            await using var ctxA = CreateContext();
            await using var ctxB = CreateContext();
            var (sutA, _) = ServiceFactory.CreateTaskService(ctxA, seed.Owner.Id);
            var (sutB, _) = ServiceFactory.CreateTaskService(ctxB, seed.Owner.Id);

            var tokenA = await ReadRowVersionAsync(moveA.Id);
            var tokenB = await ReadRowVersionAsync(moveB.Id);

            //Mindkettő az oszlop ELEJÉRE: AfterTaskId nélkül.
            var outcomes = await Task.WhenAll(
                RunAsync(() => sutA.MoveTaskAsync(seed.Project.Id, moveA.Id, new MoveTaskDto
                {
                    ColumnId = seed.Column.Id, AfterTaskId = null, RowVersion = tokenA
                })),
                RunAsync(() => sutB.MoveTaskAsync(seed.Project.Id, moveB.Id, new MoveTaskDto
                {
                    ColumnId = seed.Column.Id, AfterTaskId = null, RowVersion = tokenB
                })));

            //Ami elbukik, az KEZELT hibát ad - belső szerverhiba itt sem engedhető meg.
            foreach (var failure in outcomes.Where(o => o != null))
                Assert.IsAssignableFrom<AppException>(failure!);

            await using var verify = CreateContext();
            var tasks = await verify.ProjectTasks.AsNoTracking()
                .Where(t => t.ColumnId == seed.Column.Id)
                .ToListAsync();

            //Semmi nem veszett el, és az elejére került feladatok tényleg a horgony ELÉ
            //kerültek - vagyis a vállalt pontatlanság itt is csak a két mozgatott feladat
            //egymás-közti sorrendje, nem a többiekhez való viszonya.
            Assert.Equal(4, tasks.Count);   // a seed egy taskja + a három felvett

            var persistedAnchor = tasks.First(t => t.Id == anchor.Id);
            var moved = tasks
                .Where(t => t.Id == moveA.Id || t.Id == moveB.Id)
                .Where(t => t.Position != "0|z00001" && t.Position != "0|z00002")
                .ToList();

            Assert.NotEmpty(moved);
            Assert.All(moved, t => Assert.True(
                string.CompareOrdinal(t.Position, persistedAnchor.Position) < 0,
                $"nem a horgony ele kerult: {t.Position} vs {persistedAnchor.Position}"));
        }

        // ---------------------------------------------------------------------------------
        // 5. forgatókönyv: oszlop-átrendezés és párhuzamos feladatmozgatás
        // ---------------------------------------------------------------------------------

        [Fact]
        public async Task OrderColumns_ParhuzamosFeladatmozgatassal_NemAdBelsoHibat()
        {
            // Ez a két művelet ÁTFEDŐ halmazt ír: az átrendezés minden oszlopot és az
            // érintett oszlopok feladatainak CompletedAt mezőjét, a mozgatás egy feladatot.
            // Az átrendezés sorosított tranzakcióban fut, tehát ütközés esetén a szerver az
            // egyiket megszakítja - szerializációs hibával, VAGY holtponttal, ha a két
            // tranzakció eltérő sorrendben éri el ugyanazokat a sorokat.
            //
            // A LÉNYEGI ÁLLÍTÁS: bármelyik hibaalak esetén a hívó KEZELT hibát kap. A
            // holtpont felvétele a ConcurrencyErrors-ba pont ezért történt - nélküle egy
            // kezelhető helyzetből 500 lesz, mert a middleware minden nem AppException-t
            // belső hibaként képez le.
            //
            // AMIT EZ A TESZT NEM BIZONYÍT, és ezt tisztességes kimondani: a mérés szerint
            // (négy kör) a két művelet a GYAKORLATBAN mindig lefut ütközés nélkül - a két
            // tranzakció egyszerűen nem ér össze elég pontosan. Ez tehát NEM a holtpont- és
            // szerializációs-kezelés mérése, hanem ŐRSZEM: ha egy jövőbeli változás ezt a
            // párt ütközővé teszi, az eredmény nem lehet belső szerverhiba, és az adatbázis
            // nem maradhat inkonzisztens. A szerializációs ág tényleges mérése a sprint
            // aktiválásánál van, ahol az ütközés megbízhatóan előáll.
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "POC");

            await using var arrange = CreateContext();
            var second = new ColumnDefinition
            {
                BoardId = seed.Board.Id, Name = "Folyamatban", MapsToStatus = "InProgress", Position = 1
            };
            var third = new ColumnDefinition
            {
                BoardId = seed.Board.Id, Name = "Kesz", MapsToStatus = "Done", Position = 2
            };
            arrange.ColumnDefinitions.AddRange(second, third);
            var task = AddTask(arrange, seed, "Mozgatott", "0|i", second.Id);
            await arrange.SaveChangesAsync();

            var columns = await ReadColumnsAsync(seed.Board.Id);

            await using var ctxA = CreateContext();
            await using var ctxB = CreateContext();
            var (columnSut, _) = ServiceFactory.CreateColumnService(ctxA, seed.Owner.Id);
            var (taskSut, _) = ServiceFactory.CreateTaskService(ctxB, seed.Owner.Id);

            var token = await ReadRowVersionAsync(task.Id);

            //Az átrendezés megfordítja a két nem-backlog oszlopot, a mozgatás közben az
            //egyikbe tesz át egy feladatot.
            var order = columns
                .Select(c => new ColumnOrderDto
                {
                    Id = c.Id,
                    Position = c.Position == 0 ? 0 : (c.Position == 1 ? 2 : 1),
                    RowVersion = c.xmin
                })
                .ToList();

            var outcomes = await Task.WhenAll(
                RunAsync(() => columnSut.OrderColumnsAsync(seed.Project.Id, seed.Board.Id, order)),
                RunAsync(() => taskSut.MoveTaskAsync(seed.Project.Id, task.Id, new MoveTaskDto
                {
                    ColumnId = third.Id, AfterTaskId = null, RowVersion = token
                })));

            //EGYETLEN nem kezelt kivétel sem engedhető meg: ami elbukik, az 4xx-et ad.
            foreach (var failure in outcomes.Where(o => o != null))
                Assert.IsAssignableFrom<AppException>(failure!);

            //És az adatbázis konzisztens: a feladat pontosan egy oszlopban van, az oszlopok
            //pozíciói pedig továbbra is egyediek a táblán belül.
            await using var verify = CreateContext();
            var persisted = await verify.ProjectTasks.AsNoTracking().FirstAsync(t => t.Id == task.Id);
            Assert.NotNull(persisted.ColumnId);

            var positions = await verify.ColumnDefinitions.AsNoTracking()
                .Where(c => c.BoardId == seed.Board.Id && !c.IsDeleted)
                .Select(c => c.Position)
                .ToListAsync();

            Assert.Equal(positions.Count, positions.Distinct().Count());
        }

        // ---------------------------------------------------------------------------------

        private static ProjectTask AddTask(
            Microsoft.EntityFrameworkCore.DbContext ctx, SeededProject seed,
            string title, string position, Guid? columnId = null)
        {
            var task = new ProjectTask
            {
                ProjectId = seed.Project.Id,
                BoardId = seed.Board.Id,
                ColumnId = columnId ?? seed.Column.Id,
                TaskKey = $"{seed.Project.ProjKey}-{Guid.NewGuid().ToString()[..6]}",
                Title = title,
                Position = position,
                CreatedById = seed.Owner.Id
            };

            ctx.Add(task);
            return task;
        }

        private async Task<uint> ReadRowVersionAsync(Guid taskId)
        {
            await using var ctx = CreateContext();
            var task = await ctx.ProjectTasks.AsNoTracking().FirstAsync(t => t.Id == taskId);
            return task.xmin;
        }

        private async Task<List<ColumnDefinition>> ReadColumnsAsync(Guid boardId)
        {
            await using var ctx = CreateContext();
            return await ctx.ColumnDefinitions.AsNoTracking()
                .Where(c => c.BoardId == boardId && !c.IsDeleted)
                .OrderBy(c => c.Position)
                .ToListAsync();
        }
    }
}
