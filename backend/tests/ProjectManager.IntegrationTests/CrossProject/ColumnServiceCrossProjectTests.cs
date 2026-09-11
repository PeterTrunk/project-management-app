using ProjectManager.API.Common.Exceptions;
using ProjectManager.API.DTOs.Columns;
using ProjectManager.API.Model;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.CrossProject
{
    /// <summary>
    /// Az oszlop-műveletek két lépcsőben scope-olnak: 
    /// A board a projekthez, az oszlop a boardhoz. 
    /// Mindkét lépcsőt külön kell mérni - egy hiányzó board-szűrés ugyanúgy idegen projekt adatához enged, 
    /// mint egy hiányzó oszlop-szűrés.
    /// </summary>
    public class ColumnServiceCrossProjectTests : CrossProjectTestBase
    {
        public ColumnServiceCrossProjectTests(PostgresFixture fixture) : base(fixture) { }

        [Fact]
        public async Task GetColumnsAsync_ForeignBoard_ThrowsNotFound()
        {
            var seed = await SeedTwoProjectsAsync();
            await using var context = CreateContext();
            var (sut, _) = ServiceFactory.CreateColumnService(context, seed.A.Owner.Id);

            await Assert.ThrowsAsync<NotFoundException>(
                () => sut.GetColumnsAsync(seed.A.Project.Id, seed.B.Board.Id));
        }

        [Fact]
        public async Task CreateColumnAsync_ForeignBoard_ThrowsNotFound()
        {
            var seed = await SeedTwoProjectsAsync();
            await using var context = CreateContext();
            var (sut, _) = ServiceFactory.CreateColumnService(context, seed.A.Owner.Id);

            await Assert.ThrowsAsync<NotFoundException>(
                () => sut.CreateColumnAsync(seed.A.Project.Id, seed.B.Board.Id,
                    new CreateColumnDto
                    {
                        BoardId = seed.B.Board.Id,
                        Name = "Idegen oszlop",
                        MapsToStatus = "todo",
                        Position = 1
                    }));

            //Az idegen boardon nem keletkezhetett új oszlop
            await using var verify = CreateContext();
            var columnCount = verify.ColumnDefinitions.Count(c => c.BoardId == seed.B.Board.Id);
            Assert.Equal(1, columnCount);
        }

        [Fact]
        public async Task UpdateColumnAsync_ForeignBoard_ThrowsNotFound()
        {
            var seed = await SeedTwoProjectsAsync();
            await using var context = CreateContext();
            var (sut, _) = ServiceFactory.CreateColumnService(context, seed.A.Owner.Id);

            await Assert.ThrowsAsync<NotFoundException>(
                () => sut.UpdateColumnAsync(seed.A.Project.Id, seed.B.Board.Id, seed.B.Column.Id,
                    new UpdateColumnDto { Name = "Idegen név", RowVersion = seed.B.Column.xmin }));

            await using var verify = CreateContext();
            var column = await verify.FindAsync<ColumnDefinition>(seed.B.Column.Id);
            Assert.Equal(seed.B.Column.Name, column!.Name);
        }

        [Fact]
        public async Task DeleteColumnAsync_ForeignBoard_ThrowsNotFound()
        {
            var seed = await SeedTwoProjectsAsync();
            await using var context = CreateContext();
            var (sut, _) = ServiceFactory.CreateColumnService(context, seed.A.Owner.Id);

            await Assert.ThrowsAsync<NotFoundException>(
                () => sut.DeleteColumnAsync(seed.A.Project.Id, seed.B.Board.Id, seed.B.Column.Id));

            await AssertStillExistsAsync<ColumnDefinition>(seed.B.Column.Id);
        }

        [Fact]
        public async Task OrderColumnsAsync_ForeignBoard_ThrowsNotFound()
        {
            var seed = await SeedTwoProjectsAsync();
            await using var context = CreateContext();
            var (sut, _) = ServiceFactory.CreateColumnService(context, seed.A.Owner.Id);

            await Assert.ThrowsAsync<NotFoundException>(
                () => sut.OrderColumnsAsync(seed.A.Project.Id, seed.B.Board.Id,
                    [new ColumnOrderDto { Id = seed.B.Column.Id, Position = 5, RowVersion = seed.B.Column.xmin }]));

            await using var verify = CreateContext();
            var column = await verify.FindAsync<ColumnDefinition>(seed.B.Column.Id);
            Assert.Equal(seed.B.Column.Position, column!.Position);
        }
    }
}
