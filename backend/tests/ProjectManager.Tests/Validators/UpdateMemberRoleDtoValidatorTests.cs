using FluentValidation.TestHelper;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.DTOs.Team;
using ProjectManager.API.Validators.TeamValidators;

namespace ProjectManager.Tests.Validators
{
    /// <summary>
    /// Ez a validátor a jogosultság-emelés első kapuja: ha az Owner kiosztható lenne,
    /// egy Admin átvehetné a projektet.
    /// </summary>
    public class UpdateMemberRoleDtoValidatorTests
    {
        private readonly UpdateMemberRoleDtoValidator _validator = new();

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void ProjectRole_Empty_ShouldHaveError(string role)
        {
            _validator.TestValidate(new UpdateMemberRoleDto { ProjectRole = role })
                .ShouldHaveValidationErrorFor(x => x.ProjectRole);
        }

        [Theory]
        [InlineData(ProjectRoles.Admin)]
        [InlineData(ProjectRoles.Member)]
        [InlineData(ProjectRoles.Viewer)]
        public void ProjectRole_Assignable_ShouldNotHaveError(string role)
        {
            _validator.TestValidate(new UpdateMemberRoleDto { ProjectRole = role })
                .ShouldNotHaveAnyValidationErrors();
        }

        //Az Owner a projekt létrehozójáé: a tagszerkesztő végpont nem adhatja tovább
        [Fact]
        public void ProjectRole_Owner_ShouldHaveError()
        {
            _validator.TestValidate(new UpdateMemberRoleDto { ProjectRole = ProjectRoles.Owner })
                .ShouldHaveValidationErrorFor(x => x.ProjectRole);
        }

        //A lista összehasonlítása kis- és nagybetűérzékeny
        [Theory]
        [InlineData("admin")]
        [InlineData("ADMIN")]
        [InlineData("member")]
        [InlineData("viewer")]
        [InlineData("owner")]
        public void ProjectRole_WrongCasing_ShouldHaveError(string role)
        {
            _validator.TestValidate(new UpdateMemberRoleDto { ProjectRole = role })
                .ShouldHaveValidationErrorFor(x => x.ProjectRole);
        }

        [Theory]
        [InlineData("SuperAdmin")]
        [InlineData("Root")]
        [InlineData("Guest")]
        public void ProjectRole_UnknownRole_ShouldHaveError(string role)
        {
            _validator.TestValidate(new UpdateMemberRoleDto { ProjectRole = role })
                .ShouldHaveValidationErrorFor(x => x.ProjectRole);
        }

        //Ez a teszt akkor bukik el, ha a ValidRoles bővül, de a validátor tesztjei nem követik
        [Fact]
        public void EveryAssignableRole_IsAccepted()
        {
            Assert.All(ProjectRoles.ValidRoles, role =>
                _validator.TestValidate(new UpdateMemberRoleDto { ProjectRole = role })
                    .ShouldNotHaveAnyValidationErrors());
        }
    }
}
