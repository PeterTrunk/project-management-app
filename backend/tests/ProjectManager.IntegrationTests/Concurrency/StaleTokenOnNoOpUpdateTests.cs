using ProjectManager.API.Common.Exceptions;
using ProjectManager.API.DTOs.Boards;
using ProjectManager.API.DTOs.Columns;
using ProjectManager.API.DTOs.ProjectTask;
using ProjectManager.API.DTOs.Sprints;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.Concurrency
{
    /// <summary>
    /// A konkurencia-token ellenőrzése akkor is, ha a kérés NEM változtat semmit.
    /// </summary>
    /// <remarks>
    /// <b>A rés, amit ezek a tesztek lezárnak.</b> Ha a kérés végül egyetlen tulajdonságot
    /// sem változtat meg (a módosító DTO minden mezője <c>null</c>), akkor előfordulhat,
    /// hogy az EF egyáltalán nem küld <c>UPDATE</c>-et - és ilyenkor nincs mihez hozzámérni
    /// a tokent: egy ELAVULT tokennel érkező kérés is <c>200</c>-at kap. Ez nem adatvesztés,
    /// de megtöri a <c>409</c> szerződését: a kliens abból, hogy a mentés sikerült, azt
    /// olvassa, hogy a kezében lévő token érvényes - és a következő, immár tartalmas
    /// mentését arra építi.
    ///
    /// <b>A pontos mechanizmus, mérve - és ez a tanulságos rész.</b> A token eredeti
    /// értékének átírása magát az <c>xmin</c> tulajdonságot jelöli módosítottnak, tehát az
    /// entitás <c>Modified</c> lesz. Az <c>xmin</c> viszont tároló által generált, ezért
    /// nem kerül be a <c>SET</c> záradékba - a kérdés tehát az, hogy marad-e EGYÁLTALÁN
    /// bármi, amit írni kell. És itt jön a véletlen: az
    /// <c>AppDbContext.SaveChangesAsync</c> a módosított entitásokra <c>UpdatedAt</c>
    /// bélyegzőt tesz, DE csak ha az entitásnak van ilyen tulajdonsága.
    ///
    /// A négy konkurencia-tokennel védett entitás közül háromnak (tábla, sprint, feladat)
    /// VAN <c>UpdatedAt</c> mezője, az oszlopdefiníciónak NINCS. Vagyis az első háromnál az
    /// elavult tokent eddig is elkapta a mentés - de nem tervezésből, hanem azért, mert
    /// történetesen van egy időbélyegük, amit a bélyegző mindig átír. Az oszlopnál nem volt
    /// mit írni, tehát <c>UPDATE</c> sem készült, és az ellenőrzés elmaradt.
    ///
    /// Ezért van mind a négyre teszt, pedig a mutációs próba szerint csak az oszlopé bukik
    /// el a javítás nélkül: a másik háromé azt rögzíti, hogy a védelem ne egy
    /// időbélyeg-mező MEGLÉTÉN álljon. Ha valaki egyszer elhagyja az <c>UpdatedAt</c>-et
    /// valamelyik entitásról, ezek a tesztek jeleznek.
    ///
    /// A <c>ConcurrencyTokenRoundTripTests</c> a TARTALMAS módosítást fedi; ez a kettő
    /// együtt adja ki a szerződést.
    /// </remarks>
    public class StaleTokenOnNoOpUpdateTests(PostgresFixture fixture) : DatabaseTestBase(fixture)
    {
        /// <summary>Biztosan elavult token: a valódi érték sosem 1 egy élő soron.</summary>
        private const uint StaleToken = 1u;

        [Fact]
        public async Task UpdateBoard_UresModositasElavultTokennel_Utkozest_Jelez()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "NOB");
            var (sut, _) = ServiceFactory.CreateBoardService(context, seed.Owner.Id);

            await Assert.ThrowsAsync<ConflictException>(
                () => sut.UpdateBoardAsync(seed.Project.Id, seed.Board.Id,
                    new UpdateBoardDto { RowVersion = StaleToken }));
        }

        [Fact]
        public async Task UpdateColumn_UresModositasElavultTokennel_Utkozest_Jelez()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "NOC");
            var (sut, _) = ServiceFactory.CreateColumnService(context, seed.Owner.Id);

            await Assert.ThrowsAsync<ConflictException>(
                () => sut.UpdateColumnAsync(seed.Project.Id, seed.Board.Id, seed.Column.Id,
                    new UpdateColumnDto { RowVersion = StaleToken }));
        }

        [Fact]
        public async Task UpdateSprint_UresModositasElavultTokennel_Utkozest_Jelez()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "NOS");
            var (sut, _) = ServiceFactory.CreateSprintService(context, seed.Owner.Id);

            await Assert.ThrowsAsync<ConflictException>(
                () => sut.UpdateSprintAsync(seed.Project.Id, seed.Sprint.Id,
                    new UpdateSprintDto { RowVersion = StaleToken }));
        }

        [Fact]
        public async Task UpdateTask_UresModositasElavultTokennel_Utkozest_Jelez()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "NOT");
            var (sut, _) = ServiceFactory.CreateTaskService(context, seed.Owner.Id);

            await Assert.ThrowsAsync<ConflictException>(
                () => sut.UpdateTaskAsync(seed.Project.Id, seed.Task.Id,
                    new UpdateTaskDto { RowVersion = StaleToken }));
        }

        [Fact]
        public async Task UpdateTask_UresModositasHELYESTokennel_Lefut()
        {
            // A másik fele: az őr nem lehet túl szigorú. Egy tartalom nélküli, de ÉRVÉNYES
            // tokennel érkező kérés továbbra is sikeres - különben a felület minden olyan
            // mentését elbuktatnánk, ahol a felhasználó végül nem írt át semmit.
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "NOK");
            var (sut, _) = ServiceFactory.CreateTaskService(context, seed.Owner.Id);

            var result = await sut.UpdateTaskAsync(seed.Project.Id, seed.Task.Id,
                new UpdateTaskDto { RowVersion = seed.Task.xmin });

            Assert.Equal(seed.Task.Title, result.Title);
        }
    }
}
