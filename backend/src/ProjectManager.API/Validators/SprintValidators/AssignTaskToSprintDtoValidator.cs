using FluentValidation;
using ProjectManager.API.DTOs.Sprints;

namespace ProjectManager.API.Validators.SprintValidators
{
    public class AssignTaskToSprintDtoValidator : AbstractValidator<AssignTaskToSprintDto>
    {
        public AssignTaskToSprintDtoValidator()
        {
            RuleFor(d => d.RowVersion)
                .GreaterThan(0u).WithMessage("Érvénytelen RowVersion!");
        }
    }
}
