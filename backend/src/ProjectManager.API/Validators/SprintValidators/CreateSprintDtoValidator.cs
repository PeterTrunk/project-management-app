using FluentValidation;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.DTOs.Sprints;

namespace ProjectManager.API.Validators.SprintValidators
{
    public class CreateSprintDtoValidator : AbstractValidator<CreateSprintDto>
    {
        public CreateSprintDtoValidator()
        {
            RuleFor(s => s.Name)
                .NotEmpty().WithMessage("A sprint neve kötelező!")
                .MinimumLength(3).WithMessage("A sprint neve legalább 3 karakter hosszú legyen!")
                .MaximumLength(80).WithMessage("A sprint neve maximum 80 karakter lehet!");

            RuleFor(s => s.Goal)
                .MaximumLength(500).WithMessage("A sprint célja maximum 500 karakter lehet!")
                .When(s => s.Goal != null);

            //Letrehozni CSAK Planning allapotban lehet: az allapotgep
            //(Planning -> Active -> Completed) nem indulhat kozeprol.
            //
            //Indok, es ez nem elmeleti: a CreateSprintAsync kozvetlenul a dto.State-et irta,
            //tehat egy "Active" allapotu letrehozas megkerulte az ActivateSprintAsync
            //ellenorzeset ("Mar van Aktiv sprint!"), az adatbazis szuru indexe pedig
            //hatastalan volt. Ket aktiv sprint igy az API-bol kozvetlenul letrehozhato volt,
            //amitol a felulet nemdeterminisztikusan valaszt aktiv sprintet.
            //
            //A felulet mindig "Planning"-ot kuld, tehat ez nem toresi valtozas.
            RuleFor(s => s.State)
                .NotEmpty().WithMessage("A sprint állapota kötelező!")
                .Equal(SprintStates.Planning)
                .WithMessage($"Új sprint csak \"{SprintStates.Planning}\" állapotban hozható létre! "
                           + "Az aktiválás külön művelet.");

            RuleFor(s => s.EndDate)
                .GreaterThan(s => s.StartDate)
                .When(s => s.StartDate != null && s.EndDate != null)
                .WithMessage("A befejezés dátuma nem lehet korábbi a kezdésnél!");
        }
    }
}
