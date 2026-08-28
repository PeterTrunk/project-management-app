using FluentValidation;
using ProjectManager.API.DTOs.Auth;

namespace ProjectManager.API.Validators.AuthValidators
{
    public class ForgotPasswordDtoValidator : AbstractValidator<ForgotPasswordDto>
    {
        public ForgotPasswordDtoValidator()
        {
            RuleFor(d => d.Email)
                .NotEmpty().WithMessage("Az email cím megadása kötelező!")
                .EmailAddress().WithMessage("Érvénytelen email cím formátum!");
        }
    }
}
