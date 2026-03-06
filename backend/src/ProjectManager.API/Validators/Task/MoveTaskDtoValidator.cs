using FluentValidation;
using ProjectManager.API.DTOs.Task;

namespace ProjectManager.API.Validators.Task
{
    public class MoveTaskDtoValidator : AbstractValidator<MoveTaskDto>
    {
        public MoveTaskDtoValidator()
        {
            RuleFor(d => d.ColumnId)
                .NotEmpty();

            RuleFor(d => d.Position)
                .GreaterThanOrEqualTo(0)
                .WithMessage("A pozíció nem lehet negatív");
        }
    }
}
