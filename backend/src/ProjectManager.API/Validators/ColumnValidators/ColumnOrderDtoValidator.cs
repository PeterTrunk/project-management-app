using FluentValidation;
using ProjectManager.API.DTOs.Columns;

namespace ProjectManager.API.Validators.ColumnValidators
{
    public class ColumnOrderDtoValidator : AbstractValidator<ColumnOrderDto>
    {
        public ColumnOrderDtoValidator() 
        {
            RuleFor(x => x.Id)
                .NotEmpty().WithMessage("Az oszlop azonosítója kötelező!");

            RuleFor(c => c.Position)
                .GreaterThan(0)
                .WithMessage("A 0-ás pozíció a Backlog oszlopnak van fenntartva!");

            RuleFor(c => c.RowVersion)
                .GreaterThan(0u).WithMessage("Érvénytelen RowVersion!");
        }
    }
}
