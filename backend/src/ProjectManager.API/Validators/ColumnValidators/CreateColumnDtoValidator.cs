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
        }
    }
}
