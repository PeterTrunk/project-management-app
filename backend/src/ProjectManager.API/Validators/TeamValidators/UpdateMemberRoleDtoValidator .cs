using FluentValidation;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.DTOs.Team;

namespace ProjectManager.API.Validators.TeamValidators
{
    public class UpdateMemberRoleDtoValidator : AbstractValidator<UpdateMemberRoleDto>
    {
        public UpdateMemberRoleDtoValidator()
        {
            RuleFor(x => x.ProjectRole)
                .NotEmpty()
                .WithMessage("Szerepkör megadása kötelező!")
                .Must(role => ProjectRoles.ValidRoles.Contains(role))
                .WithMessage($"Érvénytelen szerepkör! Lehetséges értékek: {ProjectRoles.Admin}, {ProjectRoles.Member}, {ProjectRoles.Viewer}");
        }
    }
}
