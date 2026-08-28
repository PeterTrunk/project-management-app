using FluentValidation;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.DTOs.Sprints;

namespace ProjectManager.API.Validators.SprintValidators
{
    public class CreateSprintDtoValidator : AbstractValidator<CreateSprintDto>
    {
        public CreateSprintDtoValidator()
        {
            RuleFor(s => s.Name)
                .NotEmpty().WithMessage("A sprint neve kötelező!")
                .MinimumLength(3).WithMessage("A sprint neve legalább 3 karakter hosszú legyen!")
                .MaximumLength(80).WithMessage("A sprint neve maximum 80 karakter lehet!");

            RuleFor(s => s.Goal)
                .MaximumLength(500).WithMessage("A sprint célja maximum 500 karakter lehet!")
                .When(s => s.Goal != null);

            RuleFor(s => s.State)
                .NotEmpty().WithMessage("A sprint állapota kötelező!")
                .Must(s => SprintStates.ValidStates.Contains(s))
                .WithMessage($"Érvénytelen sprint állapot! Lehetséges értékek: {SprintStates.Planning}, {SprintStates.Active}, {SprintStates.Completed}");

            RuleFor(s => s.EndDate)
                .GreaterThan(s => s.StartDate)
                .When(s => s.StartDate != null && s.EndDate != null)
                .WithMessage("A befejezés dátuma nem lehet korábbi a kezdésnél!");
        }
    }
}
