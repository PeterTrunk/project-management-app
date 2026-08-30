using FluentValidation;
using ProjectManager.API.DTOs.Project;

namespace ProjectManager.API.Validators.ProjectValidators
{
    public class CreateProjectDtoValidator : AbstractValidator<CreateProjectDto>
    {
        public CreateProjectDtoValidator()
        {
            RuleFor(d => d.Name)
                .NotEmpty().WithMessage("A projekt neve kötelező!")
                .MaximumLength(120).WithMessage("A projekt neve maximum 120 karakter lehet!");

            RuleFor(d => d.ProjKey)
                .NotEmpty().WithMessage("A projekt kulcs megadása kötelező!")
                .MinimumLength(2).WithMessage("A projekt kulcs legalább 2 karakter hosszú legyen!")
                .MaximumLength(10).WithMessage("A projekt kulcs maximum 10 karakter lehet!")
                .Matches("^[A-Z0-9]+$")
                .WithMessage("A projekt kulcs csak nagybetűket és számokat tartalmazhat!");

            RuleFor(d => d.Description)
                .MaximumLength(1000).WithMessage("A leírás maximum 1000 karakter lehet!")
                .When(d => d.Description != null);
        }
    }
}
