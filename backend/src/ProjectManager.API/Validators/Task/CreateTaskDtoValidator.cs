using FluentValidation;
using ProjectManager.API.DTOs.Task;

namespace ProjectManager.API.Validators.Task
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
                .Must(p => new[] { "low", "normal", "high", "critical" }.Contains(p))
                .WithMessage("Érvénytelen prioritás érték: low, normal, high, critical")
                .When(d => d.Priority != null);
        }
    }
}
