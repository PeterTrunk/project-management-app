using FluentValidation;
using ProjectManager.API.DTOs.Labels;

namespace ProjectManager.API.Validators.LabelValidators
{
    public class CreateLabelDtoValidator : AbstractValidator<CreateLabelDto>
    {
        public CreateLabelDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("A címke neve kötelező!")
                .MaximumLength(40).WithMessage("A címke neve maximum 40 karakter lehet!");

            RuleFor(x => x.Color)
                .NotEmpty().WithMessage("A szín megadása kötelező!")
                .Matches("^#[0-9A-Fa-f]{6}$")
                .WithMessage("Érvénytelen hex szín formátum (pl. #FF0000)");
        }
    }
}