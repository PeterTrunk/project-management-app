using FluentValidation;
using ProjectManager.API.DTOs.Project;

namespace ProjectManager.API.Validators.ProjectValidators
{
    public class CreateProjectDtoValidator : AbstractValidator<CreateProjectDto>
    {
        public CreateProjectDtoValidator()
        {
            RuleFor(d => d.Name)
                .NotEmpty()
                .MaximumLength(120);

            RuleFor(d => d.ProjKey)
                .NotEmpty()
                .MaximumLength(10)
                .MinimumLength(2)
                .Matches("^[A-Z0-9]+$")
                .WithMessage("A projekt kulcs csak nagybetűket és számokat tartalmazhat");
            //pl.: PM, DEV123 -> késöbb majd a task keyek: PM-1, DEV123-1

            RuleFor(d => d.Description)
                .MaximumLength(1000);
        }
    }
}
