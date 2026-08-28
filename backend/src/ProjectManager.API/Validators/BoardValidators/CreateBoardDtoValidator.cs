using FluentValidation;
using ProjectManager.API.DTOs.Boards;

namespace ProjectManager.API.Validators.BoardValidators
{
    public class CreateBoardDtoValidator : AbstractValidator<CreateBoardDto>
    {
        public CreateBoardDtoValidator()
        {
            RuleFor(b => b.Name)
                .NotEmpty().WithMessage("A board nevének megadása kötelező!")
                .MinimumLength(3).WithMessage("A board neve legalább 3 karakter hosszú legyen!")
                .MaximumLength(120).WithMessage("A board neve maximum 120 karakter lehet!");

            RuleFor(b => b.Description)
                .MaximumLength(500).WithMessage("A leírás maximum 500 karakter lehet!");
        }
    }
}
