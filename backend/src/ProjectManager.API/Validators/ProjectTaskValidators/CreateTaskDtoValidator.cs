using FluentValidation;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.DTOs.ProjectTask;

namespace ProjectManager.API.DTOs.ProjectTaskValidators
{
    public class CreateTaskDtoValidator : AbstractValidator<CreateTaskDto>
    {
        public CreateTaskDtoValidator()
        {
            RuleFor(d => d.Title)
                .NotEmpty().WithMessage("A task címe kötelező!")
                .MaximumLength(200).WithMessage("A task címe maximum 200 karakter lehet!");

            RuleFor(d => d.Description)
                .MaximumLength(250).WithMessage("A leírás maximum 250 karakter lehet!")
                .When(d => d.Description != null);

            //Érvénytelen prió elvikleg nem jöhetne létre mivel a frontend fix választást ad.
            RuleFor(d => d.Priority)
                .Must(p => TaskPrioritys.ValidPriorities.Contains(p))
                .WithMessage($"Érvénytelen prioritás! Elfogadott értékek: {TaskPrioritys.Low}, {TaskPrioritys.Medium}, {TaskPrioritys.High}, {TaskPrioritys.Critical}")
                .When(d => d.Priority != null);

            RuleFor(t => t.DueDate)
                .GreaterThan(DateTime.UtcNow)
                .WithMessage("A határidő nem lehet múltbeli dátum!")
                .When(t => t.DueDate != null);
        }
    }
}
