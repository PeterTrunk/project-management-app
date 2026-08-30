using FluentValidation;
using ProjectManager.API.DTOs.Auth;

namespace ProjectManager.API.Validators.AuthValidators
{
    public class ResendVerificationDtoValidator : AbstractValidator<ResendVerificationDto>
    {
        public ResendVerificationDtoValidator()
        {
            RuleFor(d => d.Email)
                .NotEmpty().WithMessage("Az email cím megadása kötelező!")
                .EmailAddress().WithMessage("Érvénytelen email cím formátum!");
        }
    }
}
