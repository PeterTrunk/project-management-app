using FluentValidation;
using ProjectManager.API.DTOs.Auth;

namespace ProjectManager.API.Validators.AuthValidators
{
    public class VerifyTotpDtoValidator : AbstractValidator<VerifyTotpDto>
    {
        public VerifyTotpDtoValidator()
        {
            RuleFor(d => d.Token)
                .NotEmpty().WithMessage("A TOTP kód megadása kötelező!")
                .Length(6).WithMessage("A TOTP kódnak 6 karakter hosszúnak kell lennie!")
                .Matches("^[0-9]+$").WithMessage("A TOTP kód csak számokat tartalmazhat!");
        }
    }
}
