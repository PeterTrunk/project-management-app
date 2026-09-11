using ProjectManager.API.Common.Exceptions;
using ProjectManager.API.Model;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.CrossProject
{
    /// <summary>
    /// A címke-műveleteknél KÉT idegen entitás is szóba jön: a task és maga a címke.
    /// Mindkettőt külön mérjük, mert egy hiányos szűrés bármelyik oldalon idegen projekt adatához enged.
    /// </summary>
    public class LabelServiceCrossProjectTests : CrossProjectTestBase
    {
        public LabelServiceCrossProjectTests(PostgresFixture fixture) : base(fixture) { }

        [Fact]
        public async Task DeleteLabelAsync_ForeignLabel_ThrowsNotFound()
        {
            var seed = await SeedTwoProjectsAsync();
            await using var context = CreateContext();
            var (sut, _) = ServiceFactory.CreateLabelService(context, seed.A.Owner.Id);

            await Assert.ThrowsAsync<NotFoundException>(
                () => sut.DeleteLabelAsync(seed.A.Project.Id, seed.B.Label.Id));

            await AssertStillExistsAsync<Label>(seed.B.Label.Id);
        }

        [Fact]
        public async Task AddLabelToTaskAsync_ForeignTask_ThrowsNotFound()
        {
            var seed = await SeedTwoProjectsAsync();
            await using var context = CreateContext();
            var (sut, _) = ServiceFactory.CreateLabelService(context, seed.A.Owner.Id);

            //Saját címke, idegen task
            await Assert.ThrowsAsync<NotFoundException>(
                () => sut.AddLabelToTaskAsync(seed.A.Project.Id, seed.B.Task.Id, seed.A.Label.Id));

            await AssertNoLabelOnTaskAsync(seed.B.Task.Id);
        }

        [Fact]
        public async Task AddLabelToTaskAsync_ForeignLabel_ThrowsNotFound()
        {
            var seed = await SeedTwoProjectsAsync();
            await using var context = CreateContext();
            var (sut, _) = ServiceFactory.CreateLabelService(context, seed.A.Owner.Id);

            //Saját task, idegen címke
            await Assert.ThrowsAsync<NotFoundException>(
                () => sut.AddLabelToTaskAsync(seed.A.Project.Id, seed.A.Task.Id, seed.B.Label.Id));

            await AssertNoLabelOnTaskAsync(seed.A.Task.Id);
        }

        [Fact]
        public async Task RemoveLabelFromTaskAsync_ForeignTask_ThrowsNotFound()
        {
            var seed = await SeedTwoProjectsAsync();
            await using var context = CreateContext();
            var (sut, _) = ServiceFactory.CreateLabelService(context, seed.A.Owner.Id);

            await Assert.ThrowsAsync<NotFoundException>(
                () => sut.RemoveLabelFromTaskAsync(seed.A.Project.Id, seed.B.Task.Id, seed.A.Label.Id));
        }

        private async Task AssertNoLabelOnTaskAsync(Guid taskId)
        {
            await using var verify = CreateContext();
            Assert.Equal(0, verify.LabelTasks.Count(lt => lt.TaskId == taskId));
        }
    }
}
