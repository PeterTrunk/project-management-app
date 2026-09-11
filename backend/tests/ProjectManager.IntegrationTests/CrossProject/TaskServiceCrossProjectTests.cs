using ProjectManager.API.Common.Exceptions;
using ProjectManager.API.DTOs.ProjectTask;
using ProjectManager.API.Model;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.CrossProject
{
    /// <summary>
    /// A TaskService projekt-scoping tesztjei: 
    /// Az A projekt azonosítójával a B projekt taskjához nyúlunk. 
    /// Mindegyiknek NotFoundException-nel kell elhasalnia.
    /// </summary>
    public class TaskServiceCrossProjectTests : CrossProjectTestBase
    {
        public TaskServiceCrossProjectTests(PostgresFixture fixture) : base(fixture) { }

        [Fact]
        public async Task GetTaskByIdAsync_ForeignTask_ThrowsNotFound()
        {
            var seed = await SeedTwoProjectsAsync();
            await using var context = CreateContext();
            var (sut, _) = ServiceFactory.CreateTaskService(context, seed.A.Owner.Id);

            await Assert.ThrowsAsync<NotFoundException>(
                () => sut.GetTaskByIdAsync(seed.A.Project.Id, seed.B.Task.Id));
        }

        [Fact]
        public async Task UpdateTaskAsync_ForeignTask_ThrowsNotFound()
        {
            var seed = await SeedTwoProjectsAsync();
            await using var context = CreateContext();
            var (sut, _) = ServiceFactory.CreateTaskService(context, seed.A.Owner.Id);

            await Assert.ThrowsAsync<NotFoundException>(
                () => sut.UpdateTaskAsync(seed.A.Project.Id, seed.B.Task.Id,
                    new UpdateTaskDto { Title = "Idegen módosítás", RowVersion = seed.B.Task.xmin }));

            await AssertUnchangedTitleAsync(seed.B.Task.Id, seed.B.Task.Title);
        }

        [Fact]
        public async Task MoveTaskAsync_ForeignTask_ThrowsNotFound()
        {
            var seed = await SeedTwoProjectsAsync();
            await using var context = CreateContext();
            var (sut, _) = ServiceFactory.CreateTaskService(context, seed.A.Owner.Id);

            await Assert.ThrowsAsync<NotFoundException>(
                () => sut.MoveTaskAsync(seed.A.Project.Id, seed.B.Task.Id,
                    new MoveTaskDto { ColumnId = seed.A.Column.Id, RowVersion = seed.B.Task.xmin }));

            await AssertStillExistsAsync<ProjectTask>(seed.B.Task.Id);
        }

        [Fact]
        public async Task DeleteTaskAsync_ForeignTask_ThrowsNotFound()
        {
            var seed = await SeedTwoProjectsAsync();
            await using var context = CreateContext();
            var (sut, _) = ServiceFactory.CreateTaskService(context, seed.A.Owner.Id);

            await Assert.ThrowsAsync<NotFoundException>(
                () => sut.DeleteTaskAsync(seed.A.Project.Id, seed.B.Task.Id));

            await AssertStillExistsAsync<ProjectTask>(seed.B.Task.Id);
        }

        [Fact]
        public async Task AssignTaskToBoardAsync_ForeignTask_ThrowsNotFound()
        {
            var seed = await SeedTwoProjectsAsync();
            await using var context = CreateContext();
            var (sut, _) = ServiceFactory.CreateTaskService(context, seed.A.Owner.Id);

            await Assert.ThrowsAsync<NotFoundException>(
                () => sut.AssignTaskToBoardAsync(seed.A.Project.Id, seed.B.Task.Id,
                    new AssignTaskToBoardDto { BoardId = seed.A.Board.Id, RowVersion = seed.B.Task.xmin }));

            await AssertStillExistsAsync<ProjectTask>(seed.B.Task.Id);
        }

        [Fact]
        public async Task AddAssigneeAsync_ForeignTask_ThrowsNotFound()
        {
            var seed = await SeedTwoProjectsAsync();
            await using var context = CreateContext();
            var (sut, _) = ServiceFactory.CreateTaskService(context, seed.A.Owner.Id);

            await Assert.ThrowsAsync<NotFoundException>(
                () => sut.AddAssigneeAsync(seed.A.Project.Id, seed.B.Task.Id, seed.A.Owner.Id));
        }

        [Fact]
        public async Task RemoveAssigneeAsync_ForeignTask_ThrowsNotFound()
        {
            var seed = await SeedTwoProjectsAsync();
            await using var context = CreateContext();
            var (sut, _) = ServiceFactory.CreateTaskService(context, seed.A.Owner.Id);

            await Assert.ThrowsAsync<NotFoundException>(
                () => sut.RemoveAssigneeAsync(seed.A.Project.Id, seed.B.Task.Id, seed.B.Owner.Id));
        }

        private async Task AssertUnchangedTitleAsync(Guid taskId, string expectedTitle)
        {
            await using var verify = CreateContext();
            var task = await verify.FindAsync<ProjectTask>(taskId);

            Assert.NotNull(task);
            Assert.Equal(expectedTitle, task!.Title);
        }
    }
}
