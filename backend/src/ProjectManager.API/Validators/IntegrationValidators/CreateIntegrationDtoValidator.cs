using FluentValidation;
using ProjectManager.API.DTOs.Integration;

namespace ProjectManager.API.Validators.IntegrationValidators
{
    public class CreateIntegrationDtoValidator : AbstractValidator<CreateIntegrationDto>
    {
        private static readonly string[] ValidProviders = { "GitHub", "GitLab" };

        public CreateIntegrationDtoValidator()
        {
            RuleFor(x => x.Provider)
                .NotEmpty()
                .WithMessage("Provider megadása kötelező!")
                .Must(p => ValidProviders.Contains(p))
                .WithMessage("Érvénytelen provider! Lehetséges értékek: GitHub, GitLab");

            RuleFor(x => x.RepoFullName)
                .NotEmpty()
                .WithMessage("Repository neve kötelező!")
                .MaximumLength(200)
                .WithMessage("A repository neve maximum 200 karakter lehet!")
                .Matches(@"^[a-zA-Z0-9_.-]+/[a-zA-Z0-9_.-]+$")
                .WithMessage("Érvénytelen repository formátum! Helyes formátum: owner/repo");

            RuleFor(x => x.WebhookSecret)
                .NotEmpty()
                .WithMessage("Webhook secret megadása kötelező!")
                .MinimumLength(16)
                .WithMessage("A webhook secret legalább 16 karakter kell legyen!");
        }
    }
}
