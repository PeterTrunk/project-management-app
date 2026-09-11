using ProjectManager.API.Common.Exceptions;
using ProjectManager.API.DTOs.Boards;
using ProjectManager.API.Model;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.CrossProject
{
    public class BoardServiceCrossProjectTests : CrossProjectTestBase
    {
        public BoardServiceCrossProjectTests(PostgresFixture fixture) : base(fixture) { }

        [Fact]
        public async Task UpdateBoardAsync_ForeignBoard_ThrowsNotFound()
        {
            var seed = await SeedTwoProjectsAsync();
            await using var context = CreateContext();
            var (sut, _) = ServiceFactory.CreateBoardService(context, seed.A.Owner.Id);

            await Assert.ThrowsAsync<NotFoundException>(
                () => sut.UpdateBoardAsync(seed.A.Project.Id, seed.B.Board.Id,
                    new UpdateBoardDto { Name = "Idegen átnevezés", RowVersion = seed.B.Board.xmin }));

            await using var verify = CreateContext();
            var board = await verify.FindAsync<Board>(seed.B.Board.Id);
            Assert.Equal(seed.B.Board.Name, board!.Name);
        }

        [Fact]
        public async Task DeleteBoardAsync_ForeignBoard_ThrowsNotFound()
        {
            var seed = await SeedTwoProjectsAsync();
            await using var context = CreateContext();
            var (sut, _) = ServiceFactory.CreateBoardService(context, seed.A.Owner.Id);

            await Assert.ThrowsAsync<NotFoundException>(
                () => sut.DeleteBoardAsync(seed.A.Project.Id, seed.B.Board.Id));

            await AssertStillExistsAsync<Board>(seed.B.Board.Id);
        }
    }
}
