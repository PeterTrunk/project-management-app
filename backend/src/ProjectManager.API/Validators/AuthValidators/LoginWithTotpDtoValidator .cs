using FluentValidation;
using ProjectManager.API.DTOs.Auth;

namespace ProjectManager.API.Validators.AuthValidators
{
    public class LoginWithTotpDtoValidator : AbstractValidator<LoginWithTotpDto>
    {
        public LoginWithTotpDtoValidator()
        {
            RuleFor(d => d.Email)
                .NotEmpty().WithMessage("Az email cím megadása kötelező!")
                .EmailAddress().WithMessage("Érvénytelen email cím formátum!");

            RuleFor(d => d.Password)
                .NotEmpty().WithMessage("A jelszó megadása kötelező!");

            RuleFor(d => d.TotpToken)
                .NotEmpty().WithMessage("A TOTP kód megadása kötelező!")
                .Length(6).WithMessage("A TOTP kódnak 6 karakter hosszúnak kell lennie!")
                .Matches("^[0-9]+$").WithMessage("A TOTP kód csak számokat tartalmazhat!");
        }
    }
}
