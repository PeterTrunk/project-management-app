using FluentValidation;
using ProjectManager.API.DTOs.Columns;

namespace ProjectManager.API.Validators.ColumnValidators
{
    public class ColumnOrderDtoValidator : AbstractValidator<ColumnOrderDto>
    {
        public ColumnOrderDtoValidator() 
        {
            RuleFor(x => x.Id)
                .NotEmpty();

            RuleFor(c => c.Position)
                .GreaterThan(0)
                .WithMessage("A 0-ás pozíció a Backlog oszlopnak van fenntartva!");
        }
    }
}
