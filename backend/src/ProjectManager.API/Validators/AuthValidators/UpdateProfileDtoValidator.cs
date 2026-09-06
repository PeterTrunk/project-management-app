using FluentValidation;
using ProjectManager.API.DTOs.Auth;

namespace ProjectManager.API.Validators.AuthValidators
{
    public class UpdateProfileDtoValidator : AbstractValidator<UpdateProfileDto>
    {
        public UpdateProfileDtoValidator()
        {
            RuleFor(d => d.DisplayName)
                .NotEmpty().WithMessage("A megjelenítési név megadása kötelező!")
                .MinimumLength(3).WithMessage("A névnek legalább 3 karakter hosszúnak kell lennie!")
                .MaximumLength(120).WithMessage("A név maximum 120 karakter lehet!")
                //Mélységi védelem az XSS ellen: a megjelenítendő név activity-leírásokba interpolálódik,
                //ezért a markup-karaktereket itt zárjuk ki. A kimeneti escape-elés (Svelte) az elsődleges védelem, ez a második réteg.
                .Matches(@"^[^<>&""'`]*$")
                .WithMessage("A név nem tartalmazhat < > & \" ' ` karaktereket!");
        }
    }
}
