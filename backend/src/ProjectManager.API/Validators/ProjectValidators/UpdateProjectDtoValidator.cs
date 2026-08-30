using FluentValidation;
using ProjectManager.API.DTOs.Project;

namespace ProjectManager.API.Validators.ProjectValidators
{
    public class UpdateProjectDtoValidator : AbstractValidator<UpdateProjectDto>
    {
        public UpdateProjectDtoValidator()
        {
            RuleFor(d => d.Name)
                .MaximumLength(120).WithMessage("A projekt neve maximum 120 karakter lehet!")
                .When(d => d.Name != null);

            RuleFor(d => d.Description)
                .MaximumLength(1000).WithMessage("A leírás maximum 1000 karakter lehet!")
                .When(d => d.Description != null);
        }
    }
}
