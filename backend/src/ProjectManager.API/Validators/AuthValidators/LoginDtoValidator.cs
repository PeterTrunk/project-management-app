using FluentValidation;
using ProjectManager.API.DTOs.Auth;

namespace ProjectManager.API.Validators.AuthValidators
{
    public class LoginDtoValidator : AbstractValidator<LoginDto>
    {
        public LoginDtoValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Az email cím megadása kötelező!")
                .EmailAddress().WithMessage("Érvénytelen email cím formátum!");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("A jelszó megadása közelező!");
        }
    }
}
