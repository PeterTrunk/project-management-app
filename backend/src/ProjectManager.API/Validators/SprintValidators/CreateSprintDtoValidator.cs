using FluentValidation;
using ProjectManager.API.DTOs.Boards;
using ProjectManager.API.DTOs.Sprints;

namespace ProjectManager.API.Validators.SprintValidators
{
    public class CreateSprintDtoValidator : AbstractValidator<CreateSprintDto>
    {
        private static readonly string[] ValidStates = { "Planning", "Active", "Completed" };
        public CreateSprintDtoValidator()
        {
            RuleFor(s => s.Name)
                .NotEmpty()
                .MinimumLength(3)
                .MaximumLength(80);

            RuleFor(s => s.Goal)
                .MaximumLength(500)
                .When(s => s.Goal != null);

            RuleFor(s => s.State)
                .NotEmpty()
                .Must(s => ValidStates.Contains(s))
                .WithMessage("Érvénytelen sprint állapot! (Planning, Active, Completed)");

            RuleFor(s => s.EndDate)
                .GreaterThan(s => s.StartDate)
                .When(s => s.StartDate != null && s.EndDate != null)
                .WithMessage("A befejezés dátuma nem lehet korábbi a kezdésnél!");
        }
    }
}
