using ProjectManager.API.Common.Constants;

namespace ProjectManager.Tests.Common
{
    /// <summary>
    /// A szerepkör-hierarchia a jogosultsági réteg alapja: a ProjectRoleHandler a RankOf
    /// visszaadott indexeit hasonlítja össze. Egy elrontott sorrend vagy egy -1-et nem adó
    /// ismeretlen szerepkör csendes jogosultság-emelés lenne.
    /// </summary>
    public class ProjectRolesTests
    {
        //RankOf

        [Fact]
        public void RankOf_Null_ReturnsMinusOne()
        {
            Assert.Equal(-1, ProjectRoles.RankOf(null));
        }

        [Fact]
        public void RankOf_UnknownRole_ReturnsMinusOne()
        {
            Assert.Equal(-1, ProjectRoles.RankOf("SuperAdmin"));
        }

        [Fact]
        public void RankOf_EmptyString_ReturnsMinusOne()
        {
            Assert.Equal(-1, ProjectRoles.RankOf(""));
        }

        //A JWT claim értéke a DB-ből jön; ha a kis/nagybetűs írásmód valaha eltérne,
        //a rangsor -1-et adna, azaz fail-closed lenne - ezt rögzítjük szerződésként.
        [Theory]
        [InlineData("owner")]
        [InlineData("ADMIN")]
        [InlineData("member")]
        [InlineData("viewer")]
        public void RankOf_WrongCasing_ReturnsMinusOne(string role)
        {
            Assert.Equal(-1, ProjectRoles.RankOf(role));
        }

        [Theory]
        [InlineData(ProjectRoles.Viewer, 0)]
        [InlineData(ProjectRoles.Member, 1)]
        [InlineData(ProjectRoles.Admin, 2)]
        [InlineData(ProjectRoles.Owner, 3)]
        public void RankOf_KnownRole_ReturnsHierarchyIndex(string role, int expected)
        {
            Assert.Equal(expected, ProjectRoles.RankOf(role));
        }

        //Hierarchia

        [Fact]
        public void Hierarchy_IsStrictlyIncreasing_FromViewerToOwner()
        {
            Assert.True(ProjectRoles.RankOf(ProjectRoles.Viewer) < ProjectRoles.RankOf(ProjectRoles.Member));
            Assert.True(ProjectRoles.RankOf(ProjectRoles.Member) < ProjectRoles.RankOf(ProjectRoles.Admin));
            Assert.True(ProjectRoles.RankOf(ProjectRoles.Admin) < ProjectRoles.RankOf(ProjectRoles.Owner));
        }

        [Fact]
        public void Hierarchy_ContainsEveryRoleExactlyOnce()
        {
            Assert.Equal(ProjectRoles.Hierarchy.Length, ProjectRoles.Hierarchy.Distinct().Count());
            Assert.Equal(4, ProjectRoles.Hierarchy.Length);
        }

        //Egy ismeretlen szerepkör rangja soha nem érheti el a leggyengébb ismert szerepkörét,
        //különben a `rank >= required` összehasonlítás átengedné.
        [Fact]
        public void RankOf_UnknownRole_IsBelowLowestKnownRole()
        {
            Assert.True(ProjectRoles.RankOf("Nonsense") < ProjectRoles.RankOf(ProjectRoles.Viewer));
        }

        //ValidRoles

        [Fact]
        public void ValidRoles_IsSubsetOfHierarchy()
        {
            Assert.All(ProjectRoles.ValidRoles, role => Assert.Contains(role, ProjectRoles.Hierarchy));
        }

        //Az Ownert nem lehet kiosztani: az a projekt létrehozójáé, a tagszerkesztő végpont nem adhatja tovább.
        [Fact]
        public void ValidRoles_DoesNotContainOwner()
        {
            Assert.DoesNotContain(ProjectRoles.Owner, ProjectRoles.ValidRoles);
        }

        [Fact]
        public void ValidRoles_ContainsAdminMemberAndViewer()
        {
            Assert.Equal(
                new[] { ProjectRoles.Admin, ProjectRoles.Member, ProjectRoles.Viewer },
                ProjectRoles.ValidRoles);
        }
    }
}
