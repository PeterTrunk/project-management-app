using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.Common.Exceptions;
using ProjectManager.API.Model;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.Concurrency
{
    /// <summary>
    /// Olyan invariánsok tesztjei, amelyek a sorok KÖZÖTT állnak fenn, nem egy soron -
    /// ezeket a konkurencia-token nem tudja megvédeni.
    /// </summary>
    /// <remarks>
    /// A token soronként véd: azt állítja, hogy EZ a sor nem változott. Az itt vizsgált két
    /// szabály viszont több sorra vonatkozik, és az ütköző írás egy MÁS táblában történik -
    /// az oszlop sora érintetlen marad, tehát a token egyezik, és a mentés simán lefut.
    /// Ezért kell melléjük más eszköz: sorosított tranzakció, illetve állapot-ellenőrzés.
    /// </remarks>
    public class CrossRowInvariantTests(PostgresFixture fixture) : DatabaseTestBase(fixture)
    {
        [Fact]
        public async Task DeleteColumn_FeladattalBenne_Elutasitja()
        {
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "DCT");
            var (sut, _) = ServiceFactory.CreateColumnService(context, seed.Owner.Id);

            //A felseedelt feladat az oszlopban van.
            await Assert.ThrowsAsync<ValidationException>(
                () => sut.DeleteColumnAsync(seed.Project.Id, seed.Board.Id, seed.Column.Id));

            //És nem is jelölte meg töröltnek: a sorosított tranzakció a hibaágon visszaáll.
            await using var verify = CreateContext();
            var column = await verify.ColumnDefinitions.AsNoTracking()
                .FirstAsync(c => c.Id == seed.Column.Id);

            Assert.False(column.IsDeleted);
            Assert.Null(column.DeletedAt);
        }

        [Fact]
        public async Task DeleteColumn_UresOszlopot_Torol()
        {
            // A másik fele: a tranzakcióba csomagolás nem lett túl szigorú, az üres oszlop
            // továbbra is törölhető.
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "DCU");

            await using var arrange = CreateContext();
            var empty = new ColumnDefinition
            {
                BoardId = seed.Board.Id,
                Name = "Ures oszlop",
                Position = 5,
                MapsToStatus = "Backlog"
            };
            arrange.ColumnDefinitions.Add(empty);
            await arrange.SaveChangesAsync();

            await using var actContext = CreateContext();
            var (sut, _) = ServiceFactory.CreateColumnService(actContext, seed.Owner.Id);

            await sut.DeleteColumnAsync(seed.Project.Id, seed.Board.Id, empty.Id);

            await using var verify = CreateContext();
            var column = await verify.ColumnDefinitions.AsNoTracking()
                .FirstAsync(c => c.Id == empty.Id);

            Assert.True(column.IsDeleted);
            Assert.NotNull(column.DeletedAt);
        }

        [Fact]
        public async Task CompleteSprint_MasodszorHivva_Elutasitja()
        {
            // A lezárás nem ismételhető: a második hívás ÚJRA beírná a ClosedAt bélyegzőt
            // minden befejezett feladatra, tehát elmozdítaná a lezárási időpontokat - azok
            // pedig a statisztikák bemenetei.
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "CSD");
            var (sut, _) = ServiceFactory.CreateSprintService(context, seed.Owner.Id);

            await sut.ActivateSprintAsync(seed.Project.Id, seed.Sprint.Id);
            await sut.CompleteSprintAsync(seed.Project.Id, seed.Sprint.Id, null);

            await Assert.ThrowsAsync<ValidationException>(
                () => sut.CompleteSprintAsync(seed.Project.Id, seed.Sprint.Id, null));
        }

        [Fact]
        public async Task CompleteSprint_ALezarasiIdopontokatNemMozditjaEl()
        {
            // EZ A LÉNYEGI ÁLLÍTÁS: nem csak az a baj, hogy a kérés "furcsa", hanem hogy
            // adatot ír át. Enélkül az állítás csak az lenne, hogy hibát ad.
            await using var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "CSI");

            //A feladat a sprintbe kerül, és befejezettnek számít - így kap ClosedAt-et.
            await using var arrange = CreateContext();
            var task = await arrange.ProjectTasks.FirstAsync(t => t.Id == seed.Task.Id);
            task.SprintId = seed.Sprint.Id;
            task.CompletedAt = DateTime.UtcNow;
            await arrange.SaveChangesAsync();

            //A szolgáltatás FRISS contextet kap, az előkészítés UTÁN. A seedelő context még
            //a módosítás előtti tokennel követi a feladatot, és az azonosság-feloldás miatt
            //a szolgáltatás lekérdezése is azt a példányt kapná vissza - a mentés ezért
            //ütközésre futna, a vizsgált viselkedéstől függetlenül.
            await using var actContext = CreateContext();
            var (sut, _) = ServiceFactory.CreateSprintService(actContext, seed.Owner.Id);

            await sut.ActivateSprintAsync(seed.Project.Id, seed.Sprint.Id);
            await sut.CompleteSprintAsync(seed.Project.Id, seed.Sprint.Id, null);

            await using var afterFirst = CreateContext();
            var closedAt = (await afterFirst.ProjectTasks.AsNoTracking()
                .FirstAsync(t => t.Id == seed.Task.Id)).ClosedAt;

            Assert.NotNull(closedAt);

            //A második hívás elbukik, és NEM ír át semmit.
            await Assert.ThrowsAsync<ValidationException>(
                () => sut.CompleteSprintAsync(seed.Project.Id, seed.Sprint.Id, null));

            await using var verify = CreateContext();
            var unchanged = await verify.ProjectTasks.AsNoTracking()
                .FirstAsync(t => t.Id == seed.Task.Id);

            Assert.Equal(closedAt, unchanged.ClosedAt);
            Assert.Equal(SprintStates.Completed,
                (await verify.Sprints.AsNoTracking().FirstAsync(s => s.Id == seed.Sprint.Id)).State);
        }
    }
}
