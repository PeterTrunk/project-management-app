using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectManager.API.Authorization.Handlers;
using ProjectManager.API.Authorization.Requirements;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.Model;
using ProjectManager.IntegrationTests.Infrastructure;
using System.Security.Claims;

namespace ProjectManager.IntegrationTests.Authorization
{
    /// <summary>
    /// A jogosultsági réteg belépőpontja: eldönti, hogy a hívó elég magas szerepkörrel
    /// rendelkezik-e AZ URL-ben szereplő projektben.
    ///
    /// Szándékosan valódi adatbázissal fut, nem duplával: 
    /// a döntés egy ProjectMembers lekérdezésen múlik, és pont az a kérdés, hogy a szűrés helyes-e.
    ///
    /// A legfontosabb eset a fail-closed ág. A rangsor ismeretlen szerepkörre -1-et ad; 
    /// Ha a kód csak `userRank >= requiredRank`-et nézne, két ismeretlen szerepkör esetén -1 >= -1 IGAZ lenne, és a kérés átmenne.
    /// </summary>
    public class ProjectRoleHandlerTests : DatabaseTestBase
    {
        public ProjectRoleHandlerTests(PostgresFixture fixture) : base(fixture) { }

        //Hitelesítési előfeltételek

        [Fact]
        public async Task MissingUserIdClaim_Fails()
        {
            var seed = await SeedAsync();

            var succeeded = await EvaluateAsync(seed, userId: null, routeProjectId: seed.Project.Id.ToString());

            Assert.False(succeeded);
        }

        [Fact]
        public async Task MalformedUserIdClaim_Fails()
        {
            //TryParse nélkül ez FormatException lenne, azaz 500 a jogosultsági rétegből
            var seed = await SeedAsync();

            var succeeded = await EvaluateAsync(seed, userId: "nem-egy-guid", routeProjectId: seed.Project.Id.ToString());

            Assert.False(succeeded);
        }

        [Fact]
        public async Task MissingProjectIdRouteValue_Fails()
        {
            var seed = await SeedAsync();

            var succeeded = await EvaluateAsync(seed, userId: seed.Owner.Id.ToString(), routeProjectId: null);

            Assert.False(succeeded);
        }

        [Fact]
        public async Task MalformedProjectIdRouteValue_Fails()
        {
            var seed = await SeedAsync();

            var succeeded = await EvaluateAsync(seed, userId: seed.Owner.Id.ToString(), routeProjectId: "nem-egy-guid");

            Assert.False(succeeded);
        }

        [Fact]
        public async Task UserWhoIsNotAMember_Fails()
        {
            var seed = await SeedAsync();

            //Létező felhasználó, de egy MÁSIK projekt tagja
            await using var seedContext = CreateContext();
            var outsider = await TestData.SeedProjectAsync(seedContext, "OUT");

            var succeeded = await EvaluateAsync(
                seed, userId: outsider.Owner.Id.ToString(), routeProjectId: seed.Project.Id.ToString());

            Assert.False(succeeded);
        }

        //Fail-closed

        [Fact]
        public async Task UnknownRoleInDatabase_FailsClosed()
        {
            var seed = await SeedAsync();
            await SetMemberRoleAsync(seed.Project.Id, seed.Owner.Id, "SuperAdmin");

            var succeeded = await EvaluateAsync(
                seed, userId: seed.Owner.Id.ToString(), routeProjectId: seed.Project.Id.ToString(),
                requiredRole: ProjectRoles.Viewer);

            Assert.False(succeeded);
        }

        [Fact]
        public async Task UnknownRequiredRole_FailsClosed()
        {
            var seed = await SeedAsync();

            var succeeded = await EvaluateAsync(
                seed, userId: seed.Owner.Id.ToString(), routeProjectId: seed.Project.Id.ToString(),
                requiredRole: "Superuser");

            Assert.False(succeeded);
        }

        //Ez zárja a -1 >= -1 lyukat: MINDKÉT oldal ismeretlen
        [Fact]
        public async Task BothRolesUnknown_FailsClosed()
        {
            var seed = await SeedAsync();
            await SetMemberRoleAsync(seed.Project.Id, seed.Owner.Id, "SuperAdmin");

            var succeeded = await EvaluateAsync(
                seed, userId: seed.Owner.Id.ToString(), routeProjectId: seed.Project.Id.ToString(),
                requiredRole: "Superuser");

            Assert.False(succeeded);
        }

        //A szerepkör-hierarchia

        [Theory]
        [InlineData(ProjectRoles.Owner, ProjectRoles.Admin, true)]
        [InlineData(ProjectRoles.Owner, ProjectRoles.Viewer, true)]
        [InlineData(ProjectRoles.Admin, ProjectRoles.Admin, true)]
        [InlineData(ProjectRoles.Admin, ProjectRoles.Member, true)]
        [InlineData(ProjectRoles.Member, ProjectRoles.Viewer, true)]
        [InlineData(ProjectRoles.Member, ProjectRoles.Member, true)]
        [InlineData(ProjectRoles.Viewer, ProjectRoles.Viewer, true)]
        [InlineData(ProjectRoles.Viewer, ProjectRoles.Member, false)]
        [InlineData(ProjectRoles.Member, ProjectRoles.Admin, false)]
        [InlineData(ProjectRoles.Admin, ProjectRoles.Owner, false)]
        public async Task RoleHierarchy_IsEnforced(string userRole, string requiredRole, bool expected)
        {
            var seed = await SeedAsync();
            await SetMemberRoleAsync(seed.Project.Id, seed.Owner.Id, userRole);

            var succeeded = await EvaluateAsync(
                seed, userId: seed.Owner.Id.ToString(), routeProjectId: seed.Project.Id.ToString(),
                requiredRole: requiredRole);

            Assert.Equal(expected, succeeded);
        }

        //A szerepkör összehasonlítása kis- és nagybetűérzékeny, tehát az eltérő írásmód ismeretlen szerepkörnek számít,
        //ezért vagyis fail-closed
        [Fact]
        public async Task RoleWithWrongCasing_FailsClosed()
        {
            var seed = await SeedAsync();
            await SetMemberRoleAsync(seed.Project.Id, seed.Owner.Id, "owner");

            var succeeded = await EvaluateAsync(
                seed, userId: seed.Owner.Id.ToString(), routeProjectId: seed.Project.Id.ToString(),
                requiredRole: ProjectRoles.Viewer);

            Assert.False(succeeded);
        }

        //Segédek

        private async Task<SeededProject> SeedAsync()
        {
            await using var context = CreateContext();
            return await TestData.SeedProjectAsync(context, "ROL");
        }

        private async Task SetMemberRoleAsync(Guid projectId, Guid userId, string role)
        {
            await using var context = CreateContext();
            var member = await context.ProjectMembers
                .SingleAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);

            member.ProjectRole = role;
            await context.SaveChangesAsync();
        }

        private async Task<bool> EvaluateAsync(
            SeededProject seed,
            string? userId,
            string? routeProjectId,
            string requiredRole = ProjectRoles.Admin)
        {
            await using var context = CreateContext();

            var httpContext = new DefaultHttpContext();
            if (routeProjectId != null)
                httpContext.Request.RouteValues["projectId"] = routeProjectId;

            var identity = new ClaimsIdentity();
            if (userId != null)
                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));

            var handler = new ProjectRoleHandler(
                context,
                new HttpContextAccessor { HttpContext = httpContext },
                NullLogger<ProjectRoleHandler>.Instance);

            var authContext = new AuthorizationHandlerContext(
                [new ProjectRoleRequirement(requiredRole)],
                new ClaimsPrincipal(identity),
                resource: null);

            await handler.HandleAsync(authContext);

            return authContext.HasSucceeded;
        }
    }
}
