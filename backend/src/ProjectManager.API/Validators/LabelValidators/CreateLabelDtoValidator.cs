using FluentValidation;
using ProjectManager.API.DTOs.Labels;

namespace ProjectManager.API.Validators.LabelValidators
{
    public class CreateLabelDtoValidator : AbstractValidator<CreateLabelDto>
    {
        public CreateLabelDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .MaximumLength(50);

            RuleFor(x => x.Color)
                .NotEmpty()
                .Matches("^#[0-9A-Fa-f]{6}$")
                .WithMessage("Érvénytelen hex szín formátum (pl. #FF0000)");
        }
    }
}