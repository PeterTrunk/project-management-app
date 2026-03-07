using FluentValidation;
using ProjectManager.API.DTOs.Project;

namespace ProjectManager.API.Validators.ProjectValidators
{
    public class UpdateProjectDtoValidator : AbstractValidator<UpdateProjectDto>
    {
        public UpdateProjectDtoValidator()
        {
            RuleFor(d => d.Name)
                .MaximumLength(120)
                .When(d => d.Name != null);

            RuleFor(d => d.Description)
                .MaximumLength(1000)
                .When(d => d.Name != null);
        }
    }
}
