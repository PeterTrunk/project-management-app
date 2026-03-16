using FluentValidation;
using ProjectManager.API.DTOs.Auth;

namespace ProjectManager.API.Validators.AuthValidators
{
    public class RegisterDtoValidator : AbstractValidator<RegisterDto>
    {
        public RegisterDtoValidator()
        {
            RuleFor(x =>  x.Email)
                .NotEmpty()
                .EmailAddress()
                .MaximumLength(254);

            RuleFor(x => x.DisplayName)
                .MaximumLength(120)
                .MinimumLength(3);
            
            RuleFor(x => x.Password)
                .NotEmpty()
                .MinimumLength(8).WithMessage("A jelszónak legalább 8 karakter hosszúnak kell lennie")
                .Matches("[A-Z]").WithMessage("A jelszónak tartalmaznia kell legalább egy nagybetűt!")
                .Matches("[0-9]").WithMessage("A jelszónak tartalmaznia kell legalább egy számot!")
                .Matches("[!@#$%^&*]").WithMessage("A jelszónak tartalmaznia kell legalább egy speciális karaktert (!@#$%^&*)!");
        }
    }
}
