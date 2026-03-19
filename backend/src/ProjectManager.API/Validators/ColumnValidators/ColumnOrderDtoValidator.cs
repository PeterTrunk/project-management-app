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

            RuleFor(x => x.Position)
                .GreaterThanOrEqualTo(0);
        }
    }
}
