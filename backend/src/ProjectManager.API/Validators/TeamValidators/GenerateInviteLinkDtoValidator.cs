using FluentValidation;
using ProjectManager.API.DTOs.Team;

namespace ProjectManager.API.Validators.TeamValidators
{
    public class GenerateInviteLinkDtoValidator : AbstractValidator<GenerateInviteLinkDto>
    {
        public GenerateInviteLinkDtoValidator()
        {
            RuleFor(x => x.ExpiresInDays)
                .GreaterThan(0)
                .WithMessage("A lejárati idő legalább 1 nap kell legyen!")
                .LessThanOrEqualTo(30)
                .WithMessage("A lejárati idő maximum 30 nap lehet!")
                .When(x => x.ExpiresInDays.HasValue);

            RuleFor(x => x.MaxUses)
                .GreaterThan(0)
                .WithMessage("A maximális használatok száma legalább 1 kell legyen!")
                .When(x => x.MaxUses.HasValue);
        }
    }
}
