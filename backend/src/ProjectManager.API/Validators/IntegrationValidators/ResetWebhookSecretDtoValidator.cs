using FluentValidation;
using ProjectManager.API.DTOs.Integration;

namespace ProjectManager.API.Validators.IntegrationValidators
{
    public class ResetWebhookSecretDtoValidator : AbstractValidator<ResetWebhookSecretDto>
    {
        public ResetWebhookSecretDtoValidator()
        {
            RuleFor(x => x.NewSecret)
                .NotEmpty()
                .WithMessage("Az új secret megadása kötelező!")
                .MinimumLength(16)
                .WithMessage("A secret legalább 16 karakter kell legyen!");
        }
    }
}
