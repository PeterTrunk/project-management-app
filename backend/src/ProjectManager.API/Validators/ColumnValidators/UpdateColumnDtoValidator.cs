using FluentValidation;
using ProjectManager.API.DTOs.Columns;

namespace ProjectManager.API.Validators.ColumnValidators
{
    public class UpdateColumnDtoValidator : AbstractValidator<UpdateColumnDto>
    {
        public UpdateColumnDtoValidator() 
        {
            RuleFor(c => c.Name)
                .MinimumLength(3).WithMessage("Az oszlop neve legalább 3 karakter hosszú legyen!")
                .MaximumLength(80).WithMessage("Az oszlop neve maximum 80 karakter lehet!")
                .When(c => c.Name != null);

            RuleFor(c => c.MapsToStatus)
                .MinimumLength(3).WithMessage("A státusz neve legalább 3 karakter hosszú legyen!")
                .MaximumLength(32).WithMessage("A státusz neve maximum 32 karakter lehet!")
                .When(c => c.MapsToStatus != null);

            RuleFor(c => c.RowVersion)
                .GreaterThan(0u).WithMessage("Érvénytelen RowVersion!");
        }
    }
}
