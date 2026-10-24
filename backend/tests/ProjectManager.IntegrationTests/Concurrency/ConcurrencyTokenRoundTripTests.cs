using ProjectManager.API.Common.Exceptions;
using ProjectManager.API.DTOs.Boards;
using ProjectManager.API.DTOs.Columns;
using ProjectManager.API.DTOs.Sprints;
using ProjectManager.API.DTOs.ProjectTask;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.Concurrency
{
    /// <summary>
    /// A konkurencia-token oda-vissza útjának tesztjei: a kliens visszaküldi a kapott
    /// `rowVersion` értéket, a szolgáltatás pedig beírja a változáskövetőbe eredeti
    /// értékként, hogy az EF a módosító utasítás `WHERE` záradékába tegye.
    ///
    /// **Miért van ez a tesztosztály.** A token beírása kilenc helyen történik ebben a
    /// alakban:
    ///
    /// <code>_context.Entry(entitas).OriginalValues["xmin"] = dto.RowVersion;</code>
    ///
    /// A property neve **szöveg**, tehát a fordító nem ellenőrzi. Egy elírás futásidőben
    /// `InvalidOperationException`-t dob, amiből a middleware `500`-at képez - vagyis a
    /// művelet teljesen használhatatlan lesz, nem csak a konkurencia-védelem szűnik meg.
    ///
    /// Pontosan ez történt a sprint módosításánál: ott `OriginalValues["RowVersion"]` állt,
    /// miközben az entitás tulajdonsága `xmin`. A hiba azért élhetett észrevétlenül, mert a
    /// meglévő tesztek ezeket a metódusokat **csak az elutasítási úton** hívták (idegen
    /// projekt erőforrása -> `NotFoundException`), ami a token beírása ELŐTT kiszáll. A
    /// boldog útra egyiknek sem volt tesztje.
    ///
    /// Ez az osztály ezért mindegyik metódusnál két dolgot állít:
    /// a HELYES tokennel a művelet lefut, az ELAVULT tokennel pedig ütközést jelez - az
    /// utóbbi bizonyítja, hogy a token tényleg a `WHERE` záradékba került, nem csak a
    /// beírás nem dobott.
    /// </summary>
    public class ConcurrencyTokenRoundTripTests(PostgresFixture fixture) : DatabaseTestBase(fixture)
    {
        /// <summary>Biztosan elavult token: a valódi érték sosem 0 egy létező soron.</summary>
        private const uint StaleToken = 1u;

        // --- Tábla ---

        [Fact]
        public async Task UpdateBoard_WithTheCurrentToken_Succeeds()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "TKN");
            var (sut, _) = ServiceFactory.CreateBoardService(context, seed.Owner.Id);

            var result = await sut.UpdateBoardAsync(seed.Project.Id, seed.Board.Id,
                new UpdateBoardDto { Name = "Átnevezve", RowVersion = seed.Board.xmin });

            Assert.Equal("Átnevezve", result.Name);
        }

        [Fact]
        public async Task UpdateBoard_WithAStaleToken_Conflicts()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "TKN");
            var (sut, _) = ServiceFactory.CreateBoardService(context, seed.Owner.Id);

            await Assert.ThrowsAsync<ConflictException>(
                () => sut.UpdateBoardAsync(seed.Project.Id, seed.Board.Id,
                    new UpdateBoardDto { Name = "Átnevezve", RowVersion = StaleToken }));
        }

        // --- Oszlop ---

        [Fact]
        public async Task UpdateColumn_WithTheCurrentToken_Succeeds()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "TKN");
            var (sut, _) = ServiceFactory.CreateColumnService(context, seed.Owner.Id);

            var result = await sut.UpdateColumnAsync(seed.Project.Id, seed.Board.Id, seed.Column.Id,
                new UpdateColumnDto { Name = "Átnevezve", RowVersion = seed.Column.xmin });

            Assert.Equal("Átnevezve", result.Name);
        }

        [Fact]
        public async Task UpdateColumn_WithAStaleToken_Conflicts()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "TKN");
            var (sut, _) = ServiceFactory.CreateColumnService(context, seed.Owner.Id);

            await Assert.ThrowsAsync<ConflictException>(
                () => sut.UpdateColumnAsync(seed.Project.Id, seed.Board.Id, seed.Column.Id,
                    new UpdateColumnDto { Name = "Átnevezve", RowVersion = StaleToken }));
        }

        // --- Sprint: itt volt az elírás ---

        [Fact]
        public async Task UpdateSprint_WithTheCurrentToken_Succeeds()
        {
            //Ez a teszt az, amelyik a hibát megfogta volna. A beírt property neve
            //"RowVersion" volt "xmin" helyett, és a metódus InvalidOperationException-nel
            //szállt el minden hívásnál - tehát a sprint szerkesztése egyáltalán nem működött.
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "TKN");
            var (sut, _) = ServiceFactory.CreateSprintService(context, seed.Owner.Id);

            var result = await sut.UpdateSprintAsync(seed.Project.Id, seed.Sprint.Id,
                new UpdateSprintDto { Name = "Átnevezve", RowVersion = seed.Sprint.xmin });

            Assert.Equal("Átnevezve", result.Name);
        }

        [Fact]
        public async Task UpdateSprint_WithAStaleToken_Conflicts()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "TKN");
            var (sut, _) = ServiceFactory.CreateSprintService(context, seed.Owner.Id);

            await Assert.ThrowsAsync<ConflictException>(
                () => sut.UpdateSprintAsync(seed.Project.Id, seed.Sprint.Id,
                    new UpdateSprintDto { Name = "Átnevezve", RowVersion = StaleToken }));
        }

        // --- Feladat ---

        [Fact]
        public async Task UpdateTask_WithTheCurrentToken_Succeeds()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "TKN");
            var (sut, _) = ServiceFactory.CreateTaskService(context, seed.Owner.Id);

            var result = await sut.UpdateTaskAsync(seed.Project.Id, seed.Task.Id,
                new UpdateTaskDto { Title = "Átnevezve", RowVersion = seed.Task.xmin });

            Assert.Equal("Átnevezve", result.Title);
        }

        [Fact]
        public async Task UpdateTask_WithAStaleToken_Conflicts()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "TKN");
            var (sut, _) = ServiceFactory.CreateTaskService(context, seed.Owner.Id);

            await Assert.ThrowsAsync<ConflictException>(
                () => sut.UpdateTaskAsync(seed.Project.Id, seed.Task.Id,
                    new UpdateTaskDto { Title = "Átnevezve", RowVersion = StaleToken }));
        }
    }
}
