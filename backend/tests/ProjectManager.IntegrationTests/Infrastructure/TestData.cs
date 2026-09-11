using ProjectManager.API.Common.Constants;
using ProjectManager.API.Data;
using ProjectManager.API.Model;
using ProjectManager.API.Services.LexorankService;

namespace ProjectManager.IntegrationTests.Infrastructure
{
    /// <summary>Egy felseedelt projekt és a hozzá tartozó gráf.</summary>
    public sealed record SeededProject(
        User Owner,
        Project Project,
        Board Board,
        ColumnDefinition Column,
        ProjectTask Task);

    /// <summary>
    /// Két, egymástól független projekt külön tulajdonossal - a projekt-scoping (IDOR) tesztek alapja:
    /// Az egyik projekt azonosítójával a másik entitásához nyúlunk.
    /// </summary>
    public sealed record TwoProjects(SeededProject A, SeededProject B);

    public static class TestData
    {
        private static readonly ILexorankService Lexorank = new LexorankService();

        /// <summary>
        /// Felseedeli a minimális működő projekt-gráfot.
        ///
        /// A ProjectCounter nem hagyható ki: nélküle a CounterService NotFoundException-t dob az első task létrehozásakor,
        /// és a teszt olyan hibán bukna el, aminek semmi köze a vizsgált viselkedéshez.
        /// </summary>
        public static async Task<SeededProject> SeedProjectAsync(
            AppDbContext context,
            string projKey,
            string ownerDisplayName = "Teszt Elek")
        {
            var owner = new User
            {
                Email = $"{projKey.ToLowerInvariant()}-owner@example.com",
                DisplayName = ownerDisplayName,
                PasswordHash = "nem-valodi-hash",
                IsEmailVerified = true
            };
            context.Users.Add(owner);

            var project = new Project
            {
                Name = $"{projKey} projekt",
                ProjKey = projKey,
                OwnerId = owner.Id
            };
            context.Projects.Add(project);

            context.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = project.Id,
                UserId = owner.Id,
                ProjectRole = ProjectRoles.Owner,
                JoinedAt = DateTime.UtcNow
            });

            context.ProjectCounters.Add(new ProjectCounter
            {
                ProjectId = project.Id,
                LastNum = 1
            });

            var board = new Board
            {
                ProjectId = project.Id,
                Name = "Main Board",
                IsDefault = true
            };
            context.Boards.Add(board);

            var column = new ColumnDefinition
            {
                BoardId = board.Id,
                Name = "Backlog",
                MapsToStatus = "backlog",
                Position = 0
            };
            context.ColumnDefinitions.Add(column);

            var task = new ProjectTask
            {
                ProjectId = project.Id,
                BoardId = board.Id,
                ColumnId = column.Id,
                CreatedById = owner.Id,
                TaskKey = $"{projKey}-1",
                Title = $"{projKey} első taskja",
                Position = Lexorank.GetInitialPosition(null)
            };
            context.ProjectTasks.Add(task);

            await context.SaveChangesAsync();

            return new SeededProject(owner, project, board, column, task);
        }

        /// <summary>
        /// Két teljesen független projekt, külön tulajdonossal. 
        /// A projekt-kulcsok eltérnek hogy a task kulcsok se ütközzenek.
        /// </summary>
        public static async Task<TwoProjects> SeedTwoProjectsAsync(AppDbContext context)
        {
            var a = await SeedProjectAsync(context, "AAA", "Alfa Anna");
            var b = await SeedProjectAsync(context, "BBB", "Béta Bence");
            return new TwoProjects(a, b);
        }
    }
}
