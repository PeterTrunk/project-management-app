using FluentValidation;
using ProjectManager.API.DTOs.Sprints;

namespace ProjectManager.API.Validators.SprintValidators
{
    public class UpdateSprintDtoValidator : AbstractValidator<UpdateSprintDto>
    {
        public UpdateSprintDtoValidator()
        {
            RuleFor(s => s.Name)
                .NotEmpty()
                .MinimumLength(3)
                .MaximumLength(80)
                .When(s => s.Name != null);

            RuleFor(s => s.Goal)
                .MaximumLength(500)
                .When(s => s.Goal != null);                

            RuleFor(s => s.EndDate)
                .GreaterThan(s => s.StartDate)
                .When(s => s.StartDate != null && s.EndDate != null)
                .WithMessage("A befejezés dátuma nem lehet korábbi a kezdésnél!");
        }
    }
}
