using FluentValidation;
using ProjectManager.API.DTOs.Integration;

namespace ProjectManager.API.Validators.IntegrationValidators
{
    public class SetAccessTokenDtoValidator : AbstractValidator<SetAccessTokenDto>
    {
        public SetAccessTokenDtoValidator()
        {
            //Üres érték megengedett: az a token TÖRLÉSE. Ezért nincs NotEmpty szabály,
            //csak felső hossz-korlát - a szolgáltatók tokenjei jóval rövidebbek ennél,
            //a korlát a nyilvánvalóan hibás bemenet (beillesztett fájl) ellen van.
            RuleFor(x => x.AccessToken)
                .MaximumLength(500)
                .WithMessage("A hozzáférési token maximum 500 karakter lehet!")
                .When(x => !string.IsNullOrEmpty(x.AccessToken));
        }
    }
}
