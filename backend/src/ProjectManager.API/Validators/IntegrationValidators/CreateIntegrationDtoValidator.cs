using FluentValidation;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.DTOs.Integration;

namespace ProjectManager.API.Validators.IntegrationValidators
{
    public class CreateIntegrationDtoValidator : AbstractValidator<CreateIntegrationDto>
    {
        public CreateIntegrationDtoValidator()
        {
            RuleFor(x => x.Provider)
                .NotEmpty()
                .WithMessage("Provider megadása kötelező!")
                .Must(p => GitProviders.All.Contains(p))
                .WithMessage("Érvénytelen provider! Lehetséges értékek: GitHub, GitLab");

            RuleFor(x => x.RepoFullName)
                .NotEmpty()
                .WithMessage("Repository neve kötelező!")
                .MaximumLength(200)
                .WithMessage("A repository neve maximum 200 karakter lehet!")
                //Legalább két szegmens, de lehet több: a GitLab enged alcsoportokat (csoport/alcsoport/projekt)
                .Matches(@"^[a-zA-Z0-9_.-]+(/[a-zA-Z0-9_.-]+)+$")
                .WithMessage("Érvénytelen repository formátum! Helyes formátum: tulajdonos/repository (GitLab alcsoportnál több szint is lehet)");

            RuleFor(x => x.WebhookSecret)
                .NotEmpty()
                .WithMessage("Webhook secret megadása kötelező!")
                .MinimumLength(16)
                .WithMessage("A webhook secret legalább 16 karakter kell legyen!");

            //A kiszolgáló címe opcionális: üresen a szolgáltató felhős változatát használjuk
            RuleFor(x => x.InstanceUrl)
                .MaximumLength(InstanceUrlRule.MaxLength)
                .WithMessage($"A kiszolgáló címe maximum {InstanceUrlRule.MaxLength} karakter lehet!")
                .Must(InstanceUrlRule.IsValid)
                .WithMessage(InstanceUrlRule.Message);

            //A hozzáférési token opcionális: publikus tárházból token nélkül is olvasható a
            //commitlista. Ezért nincs NotEmpty, csak felső korlát.
            RuleFor(x => x.AccessToken)
                .MaximumLength(500)
                .WithMessage("A hozzáférési token maximum 500 karakter lehet!")
                .When(x => !string.IsNullOrEmpty(x.AccessToken));

            //A felület letiltja a gombot pipa nélkül, de az API nyilvános:
            //a kikényszerítés csak itt, a szerveren valósul meg.
            RuleFor(x => x.AuthorityConfirmed)
                .Equal(true)
                .WithMessage("A repository csatlakoztatásához nyilatkoznod kell a jogosultságodról!");
        }
    }
}
