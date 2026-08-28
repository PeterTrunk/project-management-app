using FluentValidation;
using ProjectManager.API.DTOs.Auth;

namespace ProjectManager.API.Validators.AuthValidators
{
    public class ChangePasswordDtoValidator : AbstractValidator<ChangePasswordDto>
    {
        public ChangePasswordDtoValidator()
        {
            RuleFor(d => d.CurrentPassword)
                .NotEmpty().WithMessage("A jelenlegi jelszó megadása kötelező!");

            RuleFor(d => d.NewPassword)
                .NotEmpty()
                .MinimumLength(8).WithMessage("A jelszónak legalább 8 karakter hosszúnak kell lennie")
                .Matches("[A-Z]").WithMessage("A jelszónak tartalmaznia kell legalább egy nagybetűt!")
                .Matches("[0-9]").WithMessage("A jelszónak tartalmaznia kell legalább egy számot!")
                .Matches("[!@#$%^&*]").WithMessage("A jelszónak tartalmaznia kell legalább egy speciális karaktert (!@#$%^&*)!");
        }
    }
}
