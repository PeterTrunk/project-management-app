using FluentValidation;
using ProjectManager.API.DTOs.Auth;

namespace ProjectManager.API.Validators.AuthValidators
{
    public class RegisterDtoValidator : AbstractValidator<RegisterDto>
    {
        public RegisterDtoValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Az email cím megadása kötelező!")
                .EmailAddress().WithMessage("Érvénytelen email cím formátum!")
                .MaximumLength(254).WithMessage("Az email cím maximum 254 karakter lehet!");

            RuleFor(x => x.DisplayName)
                .NotEmpty().WithMessage("A megjelenítési név megadása kötelező!")
                .MinimumLength(3).WithMessage("A névnek legalább 3 karakter hosszúnak kell lennie!")
                .MaximumLength(120).WithMessage("A név maximum 120 karakter lehet!");

            RuleFor(x => x.Password)
                .NotEmpty()
                .MinimumLength(8).WithMessage("A jelszónak legalább 8 karakter hosszúnak kell lennie")
                .Matches("[A-Z]").WithMessage("A jelszónak tartalmaznia kell legalább egy nagybetűt!")
                .Matches("[0-9]").WithMessage("A jelszónak tartalmaznia kell legalább egy számot!")
                .Matches("[!@#$%^&*]").WithMessage("A jelszónak tartalmaznia kell legalább egy speciális karaktert (!@#$%^&*)!");
        }
    }
}
