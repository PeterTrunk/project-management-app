using ProjectManager.API.Common.Exceptions;
using ProjectManager.API.DTOs.Sprints;
using ProjectManager.API.Model;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.CrossProject
{
    public class SprintServiceCrossProjectTests : CrossProjectTestBase
    {
        public SprintServiceCrossProjectTests(PostgresFixture fixture) : base(fixture) { }

        [Fact]
        public async Task UpdateSprintAsync_ForeignSprint_ThrowsNotFound()
        {
            var seed = await SeedTwoProjectsAsync();
            await using var context = CreateContext();
            var (sut, _) = ServiceFactory.CreateSprintService(context, seed.A.Owner.Id);

            await Assert.ThrowsAsync<NotFoundException>(
                () => sut.UpdateSprintAsync(seed.A.Project.Id, seed.B.Sprint.Id,
                    new UpdateSprintDto { Name = "Idegen átnevezés", RowVersion = seed.B.Sprint.xmin }));

            await using var verify = CreateContext();
            var sprint = await verify.FindAsync<Sprint>(seed.B.Sprint.Id);
            Assert.Equal(seed.B.Sprint.Name, sprint!.Name);
        }

        [Fact]
        public async Task DeleteSprintAsync_ForeignSprint_ThrowsNotFound()
        {
            var seed = await SeedTwoProjectsAsync();
            await using var context = CreateContext();
            var (sut, _) = ServiceFactory.CreateSprintService(context, seed.A.Owner.Id);

            await Assert.ThrowsAsync<NotFoundException>(
                () => sut.DeleteSprintAsync(seed.A.Project.Id, seed.B.Sprint.Id));

            await AssertStillExistsAsync<Sprint>(seed.B.Sprint.Id);
        }

        [Fact]
        public async Task ActivateSprintAsync_ForeignSprint_ThrowsNotFound()
        {
            var seed = await SeedTwoProjectsAsync();
            await using var context = CreateContext();
            var (sut, _) = ServiceFactory.CreateSprintService(context, seed.A.Owner.Id);

            await Assert.ThrowsAsync<NotFoundException>(
                () => sut.ActivateSprintAsync(seed.A.Project.Id, seed.B.Sprint.Id));

            await AssertSprintStateUnchangedAsync(seed.B.Sprint.Id, seed.B.Sprint.State);
        }

        [Fact]
        public async Task PlanSprintAsync_ForeignSprint_ThrowsNotFound()
        {
            var seed = await SeedTwoProjectsAsync();
            await using var context = CreateContext();
            var (sut, _) = ServiceFactory.CreateSprintService(context, seed.A.Owner.Id);

            await Assert.ThrowsAsync<NotFoundException>(
                () => sut.PlanSprintAsync(seed.A.Project.Id, seed.B.Sprint.Id));

            await AssertSprintStateUnchangedAsync(seed.B.Sprint.Id, seed.B.Sprint.State);
        }

        [Fact]
        public async Task CompleteSprintAsync_ForeignSprint_ThrowsNotFound()
        {
            var seed = await SeedTwoProjectsAsync();
            await using var context = CreateContext();
            var (sut, _) = ServiceFactory.CreateSprintService(context, seed.A.Owner.Id);

            await Assert.ThrowsAsync<NotFoundException>(
                () => sut.CompleteSprintAsync(seed.A.Project.Id, seed.B.Sprint.Id, null));

            await AssertSprintStateUnchangedAsync(seed.B.Sprint.Id, seed.B.Sprint.State);
        }

        [Fact]
        public async Task GetUnfinishedTasksAsync_ForeignSprint_ThrowsNotFound()
        {
            var seed = await SeedTwoProjectsAsync();
            await using var context = CreateContext();
            var (sut, _) = ServiceFactory.CreateSprintService(context, seed.A.Owner.Id);

            await Assert.ThrowsAsync<NotFoundException>(
                () => sut.GetUnfinishedTasksAsync(seed.A.Project.Id, seed.B.Sprint.Id));
        }

        [Fact]
        public async Task AssignTaskToSprintAsync_ForeignTask_ThrowsNotFound()
        {
            var seed = await SeedTwoProjectsAsync();
            await using var context = CreateContext();
            var (sut, _) = ServiceFactory.CreateSprintService(context, seed.A.Owner.Id);

            //Az A projekt sprintjébe próbáljuk betenni a B projekt taskját
            await Assert.ThrowsAsync<NotFoundException>(
                () => sut.AssignTaskToSprintAsync(seed.A.Project.Id, seed.B.Task.Id, seed.A.Sprint.Id,
                    new AssignTaskToSprintDto { RowVersion = seed.B.Task.xmin }));

            await AssertTaskHasNoSprintAsync(seed.B.Task.Id);
        }

        [Fact]
        public async Task RemoveTaskFromSprintAsync_ForeignTask_ThrowsNotFound()
        {
            var seed = await SeedTwoProjectsAsync();
            await using var context = CreateContext();
            var (sut, _) = ServiceFactory.CreateSprintService(context, seed.A.Owner.Id);

            await Assert.ThrowsAsync<NotFoundException>(
                () => sut.RemoveTaskFromSprintAsync(seed.A.Project.Id, seed.B.Task.Id,
                    new AssignTaskToSprintDto { RowVersion = seed.B.Task.xmin }));
        }

        private async Task AssertSprintStateUnchangedAsync(Guid sprintId, string expectedState)
        {
            await using var verify = CreateContext();
            var sprint = await verify.FindAsync<Sprint>(sprintId);

            Assert.NotNull(sprint);
            Assert.Equal(expectedState, sprint!.State);
        }

        private async Task AssertTaskHasNoSprintAsync(Guid taskId)
        {
            await using var verify = CreateContext();
            var task = await verify.FindAsync<ProjectTask>(taskId);

            Assert.NotNull(task);
            Assert.Null(task!.SprintId);
        }
    }
}
