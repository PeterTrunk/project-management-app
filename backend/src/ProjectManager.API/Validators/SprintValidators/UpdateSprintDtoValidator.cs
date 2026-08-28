using FluentValidation;
using ProjectManager.API.DTOs.Sprints;

namespace ProjectManager.API.Validators.SprintValidators
{
    public class UpdateSprintDtoValidator : AbstractValidator<UpdateSprintDto>
    {
        public UpdateSprintDtoValidator()
        {
            RuleFor(s => s.Name)
                .NotEmpty().WithMessage("A sprint neve nem lehet üres!")
                .MinimumLength(3).WithMessage("A sprint neve legalább 3 karakter hosszú legyen!")
                .MaximumLength(80).WithMessage("A sprint neve maximum 80 karakter lehet!")
                .When(s => s.Name != null);

            RuleFor(s => s.Goal)
                .MaximumLength(500).WithMessage("A sprint célja maximum 500 karakter lehet!")
                .When(s => s.Goal != null);

            RuleFor(s => s.EndDate)
                .GreaterThan(s => s.StartDate)
                .When(s => s.StartDate != null && s.EndDate != null)
                .WithMessage("A befejezés dátuma nem lehet korábbi a kezdésnél!");

            RuleFor(s => s.RowVersion)
                .GreaterThan(0u).WithMessage("Érvénytelen RowVersion!");
        }
    }
}
