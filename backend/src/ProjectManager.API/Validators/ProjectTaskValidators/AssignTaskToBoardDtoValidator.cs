using FluentValidation;
using ProjectManager.API.DTOs.ProjectTask;

namespace ProjectManager.API.Validators.ProjectTaskValidators
{
    public class AssignTaskToBoardDtoValidator : AbstractValidator<AssignTaskToBoardDto>
    {
        public AssignTaskToBoardDtoValidator()
        {
            RuleFor(d => d.RowVersion)
                .GreaterThan(0u).WithMessage("Érvénytelen RowVersion!");
        }
    }
}
