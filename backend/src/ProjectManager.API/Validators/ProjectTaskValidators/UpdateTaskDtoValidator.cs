using FluentValidation;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.DTOs.ProjectTask;

namespace ProjectManager.API.DTOs.ProjectTaskValidators
{
    public class UpdateTaskDtoValidator : AbstractValidator<UpdateTaskDto>
    {
        public UpdateTaskDtoValidator()
        {
            RuleFor(d => d.Title)
                .MaximumLength(200).WithMessage("A task címe maximum 200 karakter lehet!")
                .When(d => d.Title != null);

            RuleFor(d => d.Description)
                .MaximumLength(250).WithMessage("A leírás maximum 250 karakter lehet!")
                .When(d => d.Description != null);

            //Érvénytelen prió elvikleg nem jöhetne létre mivel a frontend fix választást ad.
            RuleFor(d => d.Priority)
                .Must(p => TaskPrioritys.ValidPriorities.Contains(p))
                .WithMessage($"Érvénytelen prioritás! Elfogadott értékek: {TaskPrioritys.Low}, {TaskPrioritys.Medium}, {TaskPrioritys.High}, {TaskPrioritys.Critical}")
                .When(d => d.Priority != null);

            RuleFor(d => d.RowVersion)
                .GreaterThan(0u).WithMessage("Érvénytelen RowVersion!");
        }
    }
}
