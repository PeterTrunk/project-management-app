using FluentValidation;

namespace ProjectManager.API.DTOs.ProjectTask
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
