using FluentValidation;
using ProjectManager.API.DTOs.Columns;

namespace ProjectManager.API.Validators.ColumnValidators
{
    public class UpdateColumnDtoValidator : AbstractValidator<UpdateColumnDto>
    {
        public UpdateColumnDtoValidator() 
        {
            RuleFor(c => c.Name)
                .MinimumLength(3)
                .MaximumLength(80)
                .When(c => c.Name != null);

            RuleFor(c => c.MapsToStatus)
                .MinimumLength(3)
                .MaximumLength(32)
                .When(c => c.MapsToStatus != null);
        }
    }
}
