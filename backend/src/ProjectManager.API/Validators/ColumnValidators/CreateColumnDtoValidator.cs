using FluentValidation;
using ProjectManager.API.DTOs.Columns;

namespace ProjectManager.API.Validators.ColumnValidators
{
    public class CreateColumnDtoValidator : AbstractValidator<CreateColumnDto>
    {
        public CreateColumnDtoValidator()
        {
            RuleFor(c => c.Name)
                .NotEmpty()
                .MinimumLength(3)
                .MaximumLength(80);

            RuleFor(c => c.MapsToStatus)
                .NotEmpty()
                .MinimumLength(3)
                .MaximumLength(32);

            RuleFor(c => c.Position)
                .GreaterThan(0)
                .WithMessage("A 0-ás pozíció a Backlog oszlopnak van fenntartva!");
        }
    }
}
