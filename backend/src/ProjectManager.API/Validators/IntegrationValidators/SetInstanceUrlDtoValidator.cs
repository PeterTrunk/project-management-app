using FluentValidation;
using ProjectManager.API.DTOs.Integration;

namespace ProjectManager.API.Validators.IntegrationValidators
{
    public class SetInstanceUrlDtoValidator : AbstractValidator<SetInstanceUrlDto>
    {
        public SetInstanceUrlDtoValidator()
        {
            //Üres érték megengedett: az a felhős változatra való visszaállás
            RuleFor(x => x.InstanceUrl)
                .MaximumLength(InstanceUrlRule.MaxLength)
                .WithMessage($"A kiszolgáló címe maximum {InstanceUrlRule.MaxLength} karakter lehet!")
                .Must(InstanceUrlRule.IsValid)
                .WithMessage(InstanceUrlRule.Message);
        }
    }
}
