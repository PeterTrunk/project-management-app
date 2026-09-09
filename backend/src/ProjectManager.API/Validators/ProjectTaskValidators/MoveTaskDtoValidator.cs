using FluentValidation;

namespace ProjectManager.API.DTOs.ProjectTask
{
    public class MoveTaskDtoValidator : AbstractValidator<MoveTaskDto>
    {
        public MoveTaskDtoValidator()
        {
            //A NotEmpty a default(TProperty)-hoz hasonlít, ami Guid? esetén NULL, nem Guid.Empty.
            //Enélkül a csupa nullás azonosító átmenne a validáción, és csak a szolgáltatás
            //404-ese állítaná meg - 400 helyett.
            RuleFor(d => d.ColumnId)
                .NotEmpty().WithMessage("A cél oszlop megadása kötelező!")
                .NotEqual(Guid.Empty).WithMessage("A cél oszlop megadása kötelező!");

            RuleFor(d => d.RowVersion)
                .GreaterThan(0u).WithMessage("Érvénytelen RowVersion!");

            //AfterTaskId- nem szükséges itt validálni.
            //Ha null akkor első hely, különben ellenőrizve lesz hogy tényleg létező Taskrol van e szó.
        }
    }
}
