using ProjectManager.API.Common.Exceptions;
using ProjectManager.API.DTOs.Comments;
using ProjectManager.API.Model;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.CrossProject
{
    /// <summary>
    /// A hozzászólások a taskon keresztül tartoznak projekthez, a Comment entitásnak nincs saját ProjectId mezője.
    /// Ezért itt a task-szűrés a védelem: ha az elmarad, idegen projekt beszélgetése válik olvashatóvá.
    /// </summary>
    public class CommentServiceCrossProjectTests : CrossProjectTestBase
    {
        public CommentServiceCrossProjectTests(PostgresFixture fixture) : base(fixture) { }

        [Fact]
        public async Task GetCommentsAsync_ForeignTask_ThrowsNotFound()
        {
            var seed = await SeedTwoProjectsAsync();
            await using var context = CreateContext();
            var (sut, _) = ServiceFactory.CreateCommentService(context, seed.A.Owner.Id);

            await Assert.ThrowsAsync<NotFoundException>(
                () => sut.GetCommentsAsync(seed.A.Project.Id, seed.B.Task.Id));
        }

        [Fact]
        public async Task CommentOnTaskAsync_ForeignTask_ThrowsNotFound()
        {
            var seed = await SeedTwoProjectsAsync();
            await using var context = CreateContext();
            var (sut, _) = ServiceFactory.CreateCommentService(context, seed.A.Owner.Id);

            await Assert.ThrowsAsync<NotFoundException>(
                () => sut.CommentOnTaskAsync(seed.A.Project.Id, seed.B.Task.Id,
                    new CreateCommentDto { Body = "Idegen hozzászólás" }));

            //Nem keletkezhetett új hozzászólás az idegen taskon
            await using var verify = CreateContext();
            Assert.Equal(1, verify.Comments.Count(c => c.TaskId == seed.B.Task.Id));
        }

        [Fact]
        public async Task DeleteCommentFromTaskAsync_ForeignTask_ThrowsNotFound()
        {
            var seed = await SeedTwoProjectsAsync();
            await using var context = CreateContext();
            var (sut, _) = ServiceFactory.CreateCommentService(context, seed.A.Owner.Id);

            await Assert.ThrowsAsync<NotFoundException>(
                () => sut.DeleteCommentFromTaskAsync(seed.A.Project.Id, seed.B.Task.Id, seed.B.Comment.Id));

            await AssertStillExistsAsync<Comment>(seed.B.Comment.Id);
        }
    }
}
