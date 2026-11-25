using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Data;
using ProjectManager.API.Model;
using ProjectManager.API.Services.StatisticsService;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.Statistics
{
    /// <summary>
    /// A kimutatások lekérdezései.
    ///
    /// Ezek a tesztek nem a diagramokat ellenőrzik, hanem azt a három állítást, amelyen a
    /// kimutatások helyessége múlik: hogy az időszak előtt megállt munka is látszik, hogy a
    /// szabad szöveges állapotnév nem tudja elhasítani a lekérdezést, és hogy az összesítés
    /// a megfelelő kulcs szerint csoportosít.
    /// </summary>
    public class StatisticsQueryTests : DatabaseTestBase
    {
        public StatisticsQueryTests(PostgresFixture fixture) : base(fixture) { }

        private static StatisticsService Sut(AppDbContext context) => new(context);

        /// <summary>
        /// Egy 0-nál nagyobb pozíciójú oszlop a megadott állapotnévvel.
        ///
        /// A megjelenített <paramref name="name"/> és a <paramref name="mapsToStatus"/> azért
        /// külön paraméter, mert az adatbázisban is két külön dolog: a NÉV táblánként egyedi
        /// (<c>IX_ColumnDefinitions_BoardId_Name</c>), az állapotnév viszont szabad szöveg,
        /// amely ismétlődhet. Pont ez teszi lehetővé azt az esetet, amit a „Backlog" nevű
        /// állapotról szóló teszt vizsgál.
        /// </summary>
        private static async Task<ColumnDefinition> AddColumnAsync(
            AppDbContext context, Guid boardId, string mapsToStatus, int position, string? name = null)
        {
            var column = new ColumnDefinition
            {
                BoardId = boardId,
                Name = name ?? mapsToStatus,
                MapsToStatus = mapsToStatus,
                Position = position
            };

            context.ColumnDefinitions.Add(column);
            await context.SaveChangesAsync();

            return column;
        }

        /// <summary>
        /// Állapotváltozás a megadott időpontban.
        ///
        /// Az időpontot BESZÚRÁS UTÁN írjuk be, külön utasítással, és ez nem kerülőút: az
        /// <see cref="AppDbContext.SaveChangesAsync"/> minden új entitás <c>CreatedAt</c>
        /// mezőjét a mentés idejére állítja (triggerek helyett, egy helyen). Élesben ez a
        /// helyes viselkedés - az átlépés akkor történik -, egy teszt visszadátumozását viszont
        /// NÉMÁN felülírja: a sor a mai nappal kerül be, és az időszak előtti állapotot vizsgáló
        /// tesztek üres nyitóállapotot látnak.
        ///
        /// Az <c>ExecuteUpdateAsync</c> a követőt és vele a mentési logikát is kihagyja -
        /// ugyanaz a megoldás, mint a token-visszadátumozásnál a rotációs teszteknél.
        /// </summary>
        private static async Task AddHistoryAsync(
            AppDbContext context, Guid taskId, Guid? columnId, DateTime createdAt)
        {
            var history = new TaskStatusHistory
            {
                TaskId = taskId,
                ColumnId = columnId
            };

            context.TaskStatusHistories.Add(history);
            await context.SaveChangesAsync();

            await context.TaskStatusHistories
                .Where(h => h.Id == history.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    h => h.CreatedAt, DateTime.SpecifyKind(createdAt, DateTimeKind.Utc)));
        }

        //A kumulatív folyamatábra

        /// <summary>
        /// A lényegi eset: a feladat az időszak ELŐTT lépett az állapotába, és azóta nem
        /// mozdult. A korábbi megoldás csak az ablakon belüli státuszsorokat töltötte be,
        /// ezért az ilyen feladat egyetlen napon sem számolódott - vagyis a torlódást mutató
        /// sáv pont az állandó munkát hagyta ki.
        /// </summary>
        [Fact]
        public async Task CumulativeFlow_TaskThatStoppedBeforeTheWindow_IsStillCounted()
        {
            await using var context = CreateContext();
            var seeded = await TestData.SeedProjectAsync(context, "CFD");
            var inProgress = await AddColumnAsync(context, seeded.Board.Id, "In Progress", 1);

            //Az egyetlen állapotváltozás két hónapja volt
            await AddHistoryAsync(context, seeded.Task.Id, inProgress.Id,
                DateTime.UtcNow.AddDays(-60));

            var from = DateTime.UtcNow.AddDays(-7);
            var to = DateTime.UtcNow;

            var result = await Sut(context).GetCumulativeFlowAsync(seeded.Project.Id, from, to);

            Assert.NotEmpty(result);
            Assert.All(result, point =>
                Assert.Equal(1, point.StatusCounts.Single(s => s.Status == "In Progress").Count));
        }

        /// <summary>
        /// Az ablakon belüli átlépés a megfelelő naptól számít, és a korábbi állapotból
        /// kilép - a két sáv összege nem nőhet a feladatok számánál nagyobbra.
        /// </summary>
        [Fact]
        public async Task CumulativeFlow_TransitionInsideTheWindow_MovesTheTaskBetweenBands()
        {
            await using var context = CreateContext();
            var seeded = await TestData.SeedProjectAsync(context, "CFM");
            var todo = await AddColumnAsync(context, seeded.Board.Id, "To Do", 1);
            var done = await AddColumnAsync(context, seeded.Board.Id, "Done", 2);

            await AddHistoryAsync(context, seeded.Task.Id, todo.Id, DateTime.UtcNow.AddDays(-30));
            await AddHistoryAsync(context, seeded.Task.Id, done.Id, DateTime.UtcNow.AddDays(-2));

            var result = await Sut(context).GetCumulativeFlowAsync(
                seeded.Project.Id, DateTime.UtcNow.AddDays(-5), DateTime.UtcNow);

            var first = result.First();
            var last = result.Last();

            Assert.Equal(1, first.StatusCounts.Single(s => s.Status == "To Do").Count);
            Assert.Equal(0, first.StatusCounts.Single(s => s.Status == "Done").Count);

            Assert.Equal(0, last.StatusCounts.Single(s => s.Status == "To Do").Count);
            Assert.Equal(1, last.StatusCounts.Single(s => s.Status == "Done").Count);

            //Egyetlen feladat van: egyszerre csak egy sávban állhat
            Assert.All(result, point => Assert.Equal(1, point.StatusCounts.Sum(s => s.Count)));
        }

        /// <summary>
        /// A MapsToStatus szabad szöveg: a validátor csak hosszat ellenőriz. Egy "Backlog"
        /// nevű állapot egy 0-nál nagyobb pozíciójú oszlopon ezért létrehozható - és a
        /// lekérdezés a beépített "Backlog" mellé téve kettős kulcsot kapna.
        /// </summary>
        [Fact]
        public async Task CumulativeFlow_ColumnNamedBacklog_DoesNotBreakTheQuery()
        {
            await using var context = CreateContext();
            var seeded = await TestData.SeedProjectAsync(context, "CFB");
            //A név táblánként egyedi, az állapotnév nem: a beépített "Backlog" oszlop mellé
            //így kerül egy MÁSIK oszlop, UGYANAZZAL az állapotnévvel - ez a vizsgált eset
            await AddColumnAsync(context, seeded.Board.Id, "Backlog", 1, name: "Visszatéve");

            var result = await Sut(context).GetCumulativeFlowAsync(
                seeded.Project.Id, DateTime.UtcNow.AddDays(-3), DateTime.UtcNow);

            Assert.NotEmpty(result);
            Assert.All(result, point =>
                Assert.Single(point.StatusCounts, s => s.Status == "Backlog"));
        }

        //Az átfutási idő

        private static async Task AddCommitLinkAsync(
            AppDbContext context, Guid integrationId, Guid taskId, string sha, DateTime committedAt)
        {
            context.CommitLinks.Add(new CommitLink
            {
                IntegrationId = integrationId,
                TaskId = taskId,
                CommitSha = sha,
                Message = "PMA-1 teszt commit",
                AuthorName = "Teszt Elek",
                AuthorEmail = "teszt@example.com",
                CommittedAt = DateTime.SpecifyKind(committedAt, DateTimeKind.Utc)
            });

            await context.SaveChangesAsync();
        }

        private static async Task AddPrLinkAsync(
            AppDbContext context, Guid integrationId, Guid? taskId, int prNumber,
            string state, DateTime? mergedAt)
        {
            context.PrLinks.Add(new PrLink
            {
                IntegrationId = integrationId,
                TaskId = taskId,
                PrNumber = prNumber,
                Title = $"PMA-1 pull request #{prNumber}",
                State = state,
                AuthorName = "Teszt Elek",
                MergedAt = mergedAt == null
                    ? null
                    : DateTime.SpecifyKind(mergedAt.Value, DateTimeKind.Utc)
            });

            await context.SaveChangesAsync();
        }

        private static DateTime Day(int daysAgo, int hour = 12) =>
            DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-daysAgo).AddHours(hour), DateTimeKind.Utc);

        private static (DateTime From, DateTime To) Window() =>
            (DateTime.UtcNow.Date.AddDays(-30), DateTime.UtcNow.Date);

        /// <summary>
        /// A mérés az ELSŐ committól a LEGUTÓBBI összeolvadásig tart. Az első commit azért,
        /// mert a munka akkor kezdődött; a legutóbbi összeolvadás azért, mert amíg egy újabb
        /// pull request beolvad a feladathoz, a munka nem ért véget.
        /// </summary>
        [Fact]
        public async Task CycleTime_MeasuresFromTheFirstCommitToTheMerge()
        {
            await using var context = CreateContext();
            var seeded = await TestData.SeedProjectAsync(context, "CT");
            var integration = await TestData.SeedIntegrationAsync(context, seeded.Project.Id);

            await AddCommitLinkAsync(context, integration.Id, seeded.Task.Id, new string('a', 40), Day(10));
            await AddCommitLinkAsync(context, integration.Id, seeded.Task.Id, new string('b', 40), Day(8));
            await AddPrLinkAsync(context, integration.Id, seeded.Task.Id, 42, "merged", Day(5));

            var (from, to) = Window();
            var result = await Sut(context).GetCycleTimeAsync(seeded.Project.Id, from, to);

            var point = Assert.Single(result);
            Assert.Equal(seeded.Task.TaskKey, point.TaskKey);
            Assert.Equal(42, point.PrNumber);
            Assert.Equal(2, point.CommitCount);
            Assert.Equal(Day(10), point.FirstCommitAt);
            Assert.Equal(Day(5), point.MergedAt);

            //Öt nap, órában
            Assert.Equal(120, point.Hours, precision: 1);
        }

        [Fact]
        public async Task CycleTime_SeveralMergedPullRequests_UseTheLastMerge()
        {
            await using var context = CreateContext();
            var seeded = await TestData.SeedProjectAsync(context, "CTM");
            var integration = await TestData.SeedIntegrationAsync(context, seeded.Project.Id);

            await AddCommitLinkAsync(context, integration.Id, seeded.Task.Id, new string('a', 40), Day(10));
            await AddPrLinkAsync(context, integration.Id, seeded.Task.Id, 1, "merged", Day(8));
            await AddPrLinkAsync(context, integration.Id, seeded.Task.Id, 2, "merged", Day(6));

            var (from, to) = Window();
            var result = await Sut(context).GetCycleTimeAsync(seeded.Project.Id, from, to);

            var point = Assert.Single(result);
            Assert.Equal(Day(6), point.MergedAt);

            //A sorszám ugyanahhoz a sorhoz tartozik, mint az időpont
            Assert.Equal(2, point.PrNumber);
        }

        /// <summary>
        /// A mérőszám csak akkor áll elő, ha mindkét vége megvan. A nyitott pull request még nem
        /// vég, a commit nélküli feladatnak pedig nincs kezdete - egyik sem hiba, csak nem
        /// mérhető.
        /// </summary>
        [Fact]
        public async Task CycleTime_WithoutAMergeOrWithoutACommit_IsNotMeasured()
        {
            await using var context = CreateContext();
            var seeded = await TestData.SeedProjectAsync(context, "CTN");
            var second = await TestData.AddTaskAsync(context, seeded, "CTN-2");
            var integration = await TestData.SeedIntegrationAsync(context, seeded.Project.Id);

            //Nyitott pull request, committal
            await AddCommitLinkAsync(context, integration.Id, seeded.Task.Id, new string('a', 40), Day(10));
            await AddPrLinkAsync(context, integration.Id, seeded.Task.Id, 1, "open", null);

            //Összeolvadt pull request, commit nélkül
            await AddPrLinkAsync(context, integration.Id, second.Id, 2, "merged", Day(5));

            var (from, to) = Window();
            var result = await Sut(context).GetCycleTimeAsync(seeded.Project.Id, from, to);

            Assert.Empty(result);
        }

        //Az időszak az ÖSSZEOLVADÁS napjára szűr: a mérőszám akkor válik ismertté
        [Fact]
        public async Task CycleTime_FiltersOnTheMergeDate()
        {
            await using var context = CreateContext();
            var seeded = await TestData.SeedProjectAsync(context, "CTW");
            var integration = await TestData.SeedIntegrationAsync(context, seeded.Project.Id);

            await AddCommitLinkAsync(context, integration.Id, seeded.Task.Id, new string('a', 40), Day(60));
            await AddPrLinkAsync(context, integration.Id, seeded.Task.Id, 1, "merged", Day(45));

            var sut = Sut(context);

            //Az ablakon kívüli összeolvadás kimarad, akkor is, ha a commit még régebbi
            Assert.Empty(await sut.GetCycleTimeAsync(
                seeded.Project.Id, DateTime.UtcNow.Date.AddDays(-30), DateTime.UtcNow.Date));

            //A régebbi commit viszont a mérés KEZDETE, tehát a hosszabb ablakban látszik
            var point = Assert.Single(await sut.GetCycleTimeAsync(
                seeded.Project.Id, DateTime.UtcNow.Date.AddDays(-90), DateTime.UtcNow.Date));

            Assert.Equal(15 * 24, point.Hours, precision: 1);
        }

        /// <summary>
        /// A negatív érték nem hiba, és nem is szűrjük ki a lekérdezésben: a commit időbélyege a
        /// fejlesztő gépéről jön (a git engedi átírni), és egy commit utólag is hozzárendelhető
        /// egy már összeolvadt pull requesthez. A lekérdezés nem dönt arról, mit nem mutatunk
        /// meg - azt a megjelenítés teszi, és ott kimondjuk, hány sort hagytunk ki.
        /// </summary>
        [Fact]
        public async Task CycleTime_ACommitAfterTheMerge_StaysInTheResultAsNegative()
        {
            await using var context = CreateContext();
            var seeded = await TestData.SeedProjectAsync(context, "CTX");
            var integration = await TestData.SeedIntegrationAsync(context, seeded.Project.Id);

            await AddCommitLinkAsync(context, integration.Id, seeded.Task.Id, new string('a', 40), Day(3));
            await AddPrLinkAsync(context, integration.Id, seeded.Task.Id, 1, "merged", Day(5));

            var (from, to) = Window();
            var point = Assert.Single(await Sut(context).GetCycleTimeAsync(seeded.Project.Id, from, to));

            Assert.True(point.Hours < 0);
        }

        //A kimutatások projekt-hatókörűek: a másik projekt feladata nem kerülhet bele
        [Fact]
        public async Task CycleTime_StaysInsideTheProject()
        {
            await using var context = CreateContext();
            var mine = await TestData.SeedProjectAsync(context, "CTA");
            var other = await TestData.SeedProjectAsync(context, "CTB");
            var otherIntegration = await TestData.SeedIntegrationAsync(context, other.Project.Id);

            await AddCommitLinkAsync(context, otherIntegration.Id, other.Task.Id, new string('a', 40), Day(10));
            await AddPrLinkAsync(context, otherIntegration.Id, other.Task.Id, 1, "merged", Day(5));

            var (from, to) = Window();

            Assert.Empty(await Sut(context).GetCycleTimeAsync(mine.Project.Id, from, to));
            Assert.Single(await Sut(context).GetCycleTimeAsync(other.Project.Id, from, to));
        }

        //A hozzárendeletlen pull request nem tartozik feladathoz, tehát nem is mérhető
        [Fact]
        public async Task CycleTime_IgnoresUnmatchedLinks()
        {
            await using var context = CreateContext();
            var seeded = await TestData.SeedProjectAsync(context, "CTU");
            var integration = await TestData.SeedIntegrationAsync(context, seeded.Project.Id);

            await AddCommitLinkAsync(context, integration.Id, seeded.Task.Id, new string('a', 40), Day(10));
            await AddPrLinkAsync(context, integration.Id, null, 7, "merged", Day(5));

            var (from, to) = Window();

            Assert.Empty(await Sut(context).GetCycleTimeAsync(seeded.Project.Id, from, to));
        }

        //A csapatterhelés

        /// <summary>
        /// A csoportosítás kulcsa a felhasználó azonosítója is, nem csak a megjelenítési
        /// neve: két azonos nevű tag egyébként egyetlen oszlopba olvadna össze.
        /// </summary>
        [Fact]
        public async Task Workload_TwoMembersWithTheSameDisplayName_StayTwoRows()
        {
            await using var context = CreateContext();
            var seeded = await TestData.SeedProjectAsync(context, "WL");

            var second = new User
            {
                Email = "masik@example.com",
                DisplayName = seeded.Owner.DisplayName,
                PasswordHash = "nem-valodi-hash",
                IsEmailVerified = true
            };
            context.Users.Add(second);

            context.TaskAssignments.Add(new TaskAssignment
            {
                TaskId = seeded.Task.Id,
                UserId = seeded.Owner.Id,
                AssignedAt = DateTime.UtcNow
            });
            context.TaskAssignments.Add(new TaskAssignment
            {
                TaskId = seeded.Task.Id,
                UserId = second.Id,
                AssignedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var result = await Sut(context).GetWorkloadAsync(seeded.Project.Id);

            Assert.Equal(2, result.Count);
            Assert.All(result, row => Assert.Equal(1, row.TaskCount));
        }

        /// <summary>A lezárt feladat hozzárendelése nem terheli tovább a tagot.</summary>
        [Fact]
        public async Task Workload_ClosedTask_IsNotCounted()
        {
            await using var context = CreateContext();
            var seeded = await TestData.SeedProjectAsync(context, "WLC");

            context.TaskAssignments.Add(new TaskAssignment
            {
                TaskId = seeded.Task.Id,
                UserId = seeded.Owner.Id,
                AssignedAt = DateTime.UtcNow
            });

            var task = await context.ProjectTasks.SingleAsync(t => t.Id == seeded.Task.Id);
            task.ClosedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();

            Assert.Empty(await Sut(context).GetWorkloadAsync(seeded.Project.Id));
        }

        //A burndown

        /// <summary>
        /// A lezárás KUMULATÍV: ami egyszer elkészült, az a következő napokon is az marad.
        /// A napi ciklus ezért egyszer lépked végig a rendezett lezárási napokon.
        /// </summary>
        [Fact]
        public async Task Burndown_CompletedTasks_AccumulateOverTheSprint()
        {
            await using var context = CreateContext();
            var seeded = await TestData.SeedProjectAsync(context, "BD");

            var sprint = await context.Sprints.SingleAsync(s => s.Id == seeded.Sprint.Id);
            sprint.StartDate = DateTime.UtcNow.Date.AddDays(-4);
            sprint.EndDate = DateTime.UtcNow.Date;

            var first = await context.ProjectTasks.SingleAsync(t => t.Id == seeded.Task.Id);
            first.SprintId = sprint.Id;
            first.CompletedAt = DateTime.UtcNow.Date.AddDays(-3);

            context.ProjectTasks.Add(new ProjectTask
            {
                ProjectId = seeded.Project.Id,
                BoardId = seeded.Board.Id,
                ColumnId = seeded.Column.Id,
                CreatedById = seeded.Owner.Id,
                SprintId = sprint.Id,
                TaskKey = "BD-2",
                Title = "Második",
                Position = "0|hzzzzz:",
                CompletedAt = DateTime.UtcNow.Date.AddDays(-1)
            });
            await context.SaveChangesAsync();

            var result = await Sut(context).GetBurndownAsync(seeded.Project.Id, sprint.Id);

            Assert.All(result, point => Assert.Equal(2, point.TotalTasks));
            Assert.Equal(0, result.First().CompletedTasks);
            Assert.Equal(2, result.Last().CompletedTasks);

            //Soha nem csökkenhet
            var counts = result.Select(p => p.CompletedTasks).ToList();
            Assert.Equal(counts.OrderBy(c => c), counts);

            //A hátralévő és az elkészült mindig kiadja a teljeset
            Assert.All(result, p => Assert.Equal(p.TotalTasks, p.CompletedTasks + p.RemainingTasks));
        }
    }
}
