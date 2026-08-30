using FluentValidation;

namespace ProjectManager.API.DTOs.ProjectTask
{
    public class MoveTaskDtoValidator : AbstractValidator<MoveTaskDto>
    {
        public MoveTaskDtoValidator()
        {
            RuleFor(d => d.ColumnId)
                .NotEmpty().WithMessage("A cél oszlop megadása kötelező!");

            RuleFor(d => d.RowVersion)
                .GreaterThan(0u).WithMessage("Érvénytelen RowVersion!");

            //AfterTaskId- nem szükséges itt validálni.
            //Ha null akkor első hely, különben ellenőrizve lesz hogy tényleg létező Taskrol van e szó.
        }
    }
}
