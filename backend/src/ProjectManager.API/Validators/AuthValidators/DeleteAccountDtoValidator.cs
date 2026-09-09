using FluentValidation;
using ProjectManager.API.DTOs.Auth;

namespace ProjectManager.API.Validators.AuthValidators
{
    public class DeleteAccountDtoValidator : AbstractValidator<DeleteAccountDto>
    {
        public DeleteAccountDtoValidator()
        {
            RuleFor(d => d.CurrentPassword)
                .NotEmpty().WithMessage("A jelenlegi jelszó megadása kötelező!");

            
            RuleFor(d => d.TotpToken)
                .Length(6).WithMessage("A TOTP kódnak 6 karakter hosszúnak kell lennie!")
                .Matches("^[0-9]+$").WithMessage("A TOTP kód csak számokat tartalmazhat!")
                //A null azt jelenti, hogy a fiókon nincs kétfaktoros hitelesítés.
                .When(d => d.TotpToken != null);
        }
    }
}
