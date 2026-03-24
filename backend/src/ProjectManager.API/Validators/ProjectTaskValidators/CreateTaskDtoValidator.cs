using FluentValidation;
using ProjectManager.API.DTOs.ProjectTask;

namespace ProjectManager.API.DTOs.ProjectTaskValidators
{
    public class CreateTaskDtoValidator : AbstractValidator<CreateTaskDto>
    {
        public CreateTaskDtoValidator()
        {
            RuleFor(d => d.Title)
                .NotEmpty()
                .MaximumLength(200);

            RuleFor(d => d.Description)
                .MaximumLength(250)
                .When(d => d.Description != null);

            RuleFor(d => d.Priority)
                .Must(p => new[] { "low", "normal", "medium", "high", "critical" }.Contains(p))
                .WithMessage("Érvénytelen prioritás érték, elfogadott értékek: low, normal, high, critical")
                .When(d => d.Priority != null);

            RuleFor(t => t.DueDate)
                .GreaterThan(DateTime.UtcNow)
                .WithMessage("A határidő nem lehet múltbeli dátum!")
                .When(t => t.DueDate != null);
        }
    }
}
