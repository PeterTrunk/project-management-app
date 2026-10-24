using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.Common.Exceptions;
using ProjectManager.API.DTOs.Team;
using ProjectManager.API.Model;
using ProjectManager.IntegrationTests.Infrastructure;

namespace ProjectManager.IntegrationTests.Team
{
    /// <summary>
    /// A tagkezelés rangsor-szabályának tesztjei.
    ///
    /// A szabály két összehasonlítás, és mindkettő kell:
    /// <list type="number">
    /// <item><c>RankOf(hívó) &gt; RankOf(cél)</c> - csak a nálad alacsonyabb rangút érintheted;</item>
    /// <item><c>RankOf(új szerep) &lt; RankOf(hívó)</c> - csak a magadnál alacsonyabb jogot adhatod.</item>
    /// </list>
    ///
    /// Miért integrációs teszt, és miért a szolgáltatáson: a jogosultsági kapu (a végponton
    /// lévő házirend) ezt NEM tudja eldönteni, mert a cél jelenlegi szerepkörét nem ismeri, a
    /// kért új szerepkör pedig a kérés törzsében van. A szabály tehát üzleti logika, és a
    /// hívó szerepkörének lekérdezése is adatbázist igényel.
    ///
    /// A második összehasonlítás nélkül az első megkerülhető lenne: az adminisztrátor a saját
    /// rangjára emelhetne egy tagot, és onnantól az a tag is érinthetné a többi tagot.
    /// </summary>
    public class MemberRoleRankRulesTests(PostgresFixture fixture) : DatabaseTestBase(fixture)
    {
        /// <summary>Tag felvétele a projektbe adott szerepkörrel, a teszt kiindulásához.</summary>
        private static async Task<User> AddMemberAsync(
            ProjectManager.API.Data.AppDbContext context, Guid projectId, string role, string email)
        {
            var user = new User
            {
                Email = email,
                DisplayName = email.Split('@')[0],
                PasswordHash = "nem-valodi-hash",
                IsEmailVerified = true
            };
            context.Users.Add(user);
            context.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = projectId,
                UserId = user.Id,
                ProjectRole = role
            });
            await context.SaveChangesAsync();
            return user;
        }

        private async Task<(ProjectManager.API.Data.AppDbContext Context, SeededProject Seed,
                            User Admin, User Member, User Viewer, User OtherAdmin)> SeedTeamAsync()
        {
            var context = CreateContext();
            var seed = await TestData.SeedProjectAsync(context, "RNK");

            var admin = await AddMemberAsync(context, seed.Project.Id, ProjectRoles.Admin, "admin@example.com");
            var member = await AddMemberAsync(context, seed.Project.Id, ProjectRoles.Member, "member@example.com");
            var viewer = await AddMemberAsync(context, seed.Project.Id, ProjectRoles.Viewer, "viewer@example.com");
            var otherAdmin = await AddMemberAsync(context, seed.Project.Id, ProjectRoles.Admin, "admin2@example.com");

            return (context, seed, admin, member, viewer, otherAdmin);
        }

        private async Task<string> ReadRoleAsync(Guid projectId, Guid userId)
        {
            await using var verify = CreateContext();
            return await verify.ProjectMembers
                .Where(pm => pm.ProjectId == projectId && pm.UserId == userId)
                .Select(pm => pm.ProjectRole)
                .SingleAsync();
        }

        // --- Amit az adminisztrátor MEGTEHET ---

        [Fact]
        public async Task Admin_CanDemoteAMemberToViewer()
        {
            var (context, seed, admin, member, _, _) = await SeedTeamAsync();
            await using var _c = context;
            var (sut, _) = ServiceFactory.CreateTeamService(context, admin.Id);

            await sut.UpdateMemberRoleAsync(seed.Project.Id, member.Id,
                new UpdateMemberRoleDto { ProjectRole = ProjectRoles.Viewer });

            Assert.Equal(ProjectRoles.Viewer, await ReadRoleAsync(seed.Project.Id, member.Id));
        }

        [Fact]
        public async Task Admin_CanPromoteAViewerToMember()
        {
            var (context, seed, admin, _, viewer, _) = await SeedTeamAsync();
            await using var _c = context;
            var (sut, _) = ServiceFactory.CreateTeamService(context, admin.Id);

            await sut.UpdateMemberRoleAsync(seed.Project.Id, viewer.Id,
                new UpdateMemberRoleDto { ProjectRole = ProjectRoles.Member });

            Assert.Equal(ProjectRoles.Member, await ReadRoleAsync(seed.Project.Id, viewer.Id));
        }

        [Fact]
        public async Task Admin_CanRemoveAMember()
        {
            var (context, seed, admin, member, _, _) = await SeedTeamAsync();
            await using var _c = context;
            var (sut, _) = ServiceFactory.CreateTeamService(context, admin.Id);

            await sut.RemoveMemberAsync(seed.Project.Id, member.Id);

            await using var verify = CreateContext();
            Assert.False(await verify.ProjectMembers
                .AnyAsync(pm => pm.ProjectId == seed.Project.Id && pm.UserId == member.Id));
        }

        // --- Amit az adminisztrátor NEM tehet meg ---

        [Fact]
        public async Task Admin_CannotGrantTheAdminRole()
        {
            //A második összehasonlítás: csak a magadnál alacsonyabb jogot adhatod.
            //Enélkül az adminisztrátor a saját rangjára emelhetne másokat.
            var (context, seed, admin, member, _, _) = await SeedTeamAsync();
            await using var _c = context;
            var (sut, _) = ServiceFactory.CreateTeamService(context, admin.Id);

            await Assert.ThrowsAsync<ForbiddenException>(
                () => sut.UpdateMemberRoleAsync(seed.Project.Id, member.Id,
                    new UpdateMemberRoleDto { ProjectRole = ProjectRoles.Admin }));

            Assert.Equal(ProjectRoles.Member, await ReadRoleAsync(seed.Project.Id, member.Id));
        }

        [Fact]
        public async Task Admin_CannotChangeAnotherAdminsRole()
        {
            var (context, seed, admin, _, _, otherAdmin) = await SeedTeamAsync();
            await using var _c = context;
            var (sut, _) = ServiceFactory.CreateTeamService(context, admin.Id);

            await Assert.ThrowsAsync<ForbiddenException>(
                () => sut.UpdateMemberRoleAsync(seed.Project.Id, otherAdmin.Id,
                    new UpdateMemberRoleDto { ProjectRole = ProjectRoles.Viewer }));

            Assert.Equal(ProjectRoles.Admin, await ReadRoleAsync(seed.Project.Id, otherAdmin.Id));
        }

        [Fact]
        public async Task Admin_CannotRemoveAnotherAdmin()
        {
            var (context, seed, admin, _, _, otherAdmin) = await SeedTeamAsync();
            await using var _c = context;
            var (sut, _) = ServiceFactory.CreateTeamService(context, admin.Id);

            await Assert.ThrowsAsync<ForbiddenException>(
                () => sut.RemoveMemberAsync(seed.Project.Id, otherAdmin.Id));

            await using var verify = CreateContext();
            Assert.True(await verify.ProjectMembers
                .AnyAsync(pm => pm.ProjectId == seed.Project.Id && pm.UserId == otherAdmin.Id));
        }

        [Fact]
        public async Task Admin_CannotChangeTheOwnersRole()
        {
            //A tulajdonost senki nem múlja felül, tehát ez a szabályból adódik - a meglévő,
            //explicit tulajdonos-ellenőrzés mellett is.
            var (context, seed, admin, _, _, _) = await SeedTeamAsync();
            await using var _c = context;
            var (sut, _) = ServiceFactory.CreateTeamService(context, admin.Id);

            await Assert.ThrowsAsync<ForbiddenException>(
                () => sut.UpdateMemberRoleAsync(seed.Project.Id, seed.Owner.Id,
                    new UpdateMemberRoleDto { ProjectRole = ProjectRoles.Viewer }));
        }

        // --- Amit a tulajdonos megtehet, az adminisztrátor viszont nem ---

        [Fact]
        public async Task Owner_CanGrantTheAdminRole()
        {
            var (context, seed, _, member, _, _) = await SeedTeamAsync();
            await using var _c = context;
            var (sut, _) = ServiceFactory.CreateTeamService(context, seed.Owner.Id);

            await sut.UpdateMemberRoleAsync(seed.Project.Id, member.Id,
                new UpdateMemberRoleDto { ProjectRole = ProjectRoles.Admin });

            Assert.Equal(ProjectRoles.Admin, await ReadRoleAsync(seed.Project.Id, member.Id));
        }

        [Fact]
        public async Task Owner_CanChangeAnAdminsRole()
        {
            var (context, seed, admin, _, _, _) = await SeedTeamAsync();
            await using var _c = context;
            var (sut, _) = ServiceFactory.CreateTeamService(context, seed.Owner.Id);

            await sut.UpdateMemberRoleAsync(seed.Project.Id, admin.Id,
                new UpdateMemberRoleDto { ProjectRole = ProjectRoles.Member });

            Assert.Equal(ProjectRoles.Member, await ReadRoleAsync(seed.Project.Id, admin.Id));
        }

        [Fact]
        public async Task Owner_CanRemoveAnAdmin()
        {
            var (context, seed, admin, _, _, _) = await SeedTeamAsync();
            await using var _c = context;
            var (sut, _) = ServiceFactory.CreateTeamService(context, seed.Owner.Id);

            await sut.RemoveMemberAsync(seed.Project.Id, admin.Id);

            await using var verify = CreateContext();
            Assert.False(await verify.ProjectMembers
                .AnyAsync(pm => pm.ProjectId == seed.Project.Id && pm.UserId == admin.Id));
        }

        // --- Önmagára irányuló művelet ---

        [Fact]
        public async Task Nobody_CanChangeTheirOwnRole()
        {
            //A rang nem nagyobb önmagánál, tehát ez is a szabályból adódik: nincs rá külön
            //ellenőrzés. Enélkül egy adminisztrátor tulajdonossá minősíthetné magát - azt a
            //második összehasonlítás külön is tiltja, de az önmagára irányuló művelet
            //általában sem kívánatos.
            var (context, seed, admin, _, _, _) = await SeedTeamAsync();
            await using var _c = context;
            var (sut, _) = ServiceFactory.CreateTeamService(context, admin.Id);

            await Assert.ThrowsAsync<ForbiddenException>(
                () => sut.UpdateMemberRoleAsync(seed.Project.Id, admin.Id,
                    new UpdateMemberRoleDto { ProjectRole = ProjectRoles.Viewer }));
        }

        [Fact]
        public async Task AMember_CannotChangeAnyonesRole()
        {
            //A házirend ezt már a végponton kizárná, de a szolgáltatás szintjén is tartania
            //kell: a tag rangja nem múlja felül a többi tagét.
            var (context, seed, _, member, viewer, _) = await SeedTeamAsync();
            await using var _c = context;
            var (sut, _) = ServiceFactory.CreateTeamService(context, member.Id);

            await Assert.ThrowsAsync<ForbiddenException>(
                () => sut.UpdateMemberRoleAsync(seed.Project.Id, viewer.Id,
                    new UpdateMemberRoleDto { ProjectRole = ProjectRoles.Member }));
        }

        [Fact]
        public async Task ANonMember_CannotChangeAnyonesRole()
        {
            //Fail-closed: aki nem tagja a projektnek, annak a rangja -1, tehát senkit nem
            //múl felül. A házirend ezt is kizárná, de a szabály önmagában is zárt.
            var (context, seed, _, member, _, _) = await SeedTeamAsync();
            await using var _c = context;
            var (sut, _) = ServiceFactory.CreateTeamService(context, Guid.NewGuid());

            await Assert.ThrowsAsync<ForbiddenException>(
                () => sut.UpdateMemberRoleAsync(seed.Project.Id, member.Id,
                    new UpdateMemberRoleDto { ProjectRole = ProjectRoles.Viewer }));
        }
    }
}
