using FluentValidation;
using ProjectManager.API.DTOs.Team;

namespace ProjectManager.API.Validators.TeamValidators
{
    public class UpdateMemberRoleDtoValidator : AbstractValidator<UpdateMemberRoleDto>
    {
        private static readonly string[] ValidRoles = { "Admin", "Member", "Viewer" };

        public UpdateMemberRoleDtoValidator()
        {
            RuleFor(x => x.ProjectRole)
                .NotEmpty()
                .WithMessage("Szerepkör megadása kötelező!")
                .Must(role => ValidRoles.Contains(role))
                .WithMessage("Érvénytelen szerepkör! Lehetséges értékek: Admin, Member, Viewer");
        }
    }
}
