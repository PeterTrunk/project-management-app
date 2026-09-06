using FluentValidation;
using ProjectManager.API.DTOs.Auth;

namespace ProjectManager.API.Validators.AuthValidators
{
    public class DisableTotpDtoValidator : AbstractValidator<DisableTotpDto>
    {
        public DisableTotpDtoValidator()
        {
            RuleFor(d => d.CurrentPassword)
                .NotEmpty().WithMessage("A jelenlegi jelszó megadása kötelező!");
        }
    }
}
