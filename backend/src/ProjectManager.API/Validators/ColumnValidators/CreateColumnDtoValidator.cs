using FluentValidation;
using ProjectManager.API.DTOs.Columns;

namespace ProjectManager.API.Validators.ColumnValidators
{
    public class CreateColumnDtoValidator : AbstractValidator<CreateColumnDto>
    {
        public CreateColumnDtoValidator()
        {
            RuleFor(c => c.Name)
                .NotEmpty().WithMessage("Az oszlop neve kötelező!")
                .MinimumLength(3).WithMessage("Az oszlop neve legalább 3 karakter hosszú legyen!")
                .MaximumLength(80).WithMessage("Az oszlop neve maximum 80 karakter lehet!");

            RuleFor(c => c.MapsToStatus)
                .NotEmpty().WithMessage("A státusz megadása kötelező!")
                .MinimumLength(3).WithMessage("A státusz neve legalább 3 karakter hosszú legyen!")
                .MaximumLength(32).WithMessage("A státusz neve maximum 32 karakter lehet!");

            RuleFor(c => c.Position)
                .GreaterThan(0)
                .WithMessage("A 0-ás pozíció a Backlog oszlopnak van fenntartva!");
        }
    }
}
