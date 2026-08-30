using FluentValidation;
using ProjectManager.API.DTOs.Boards;

namespace ProjectManager.API.Validators.BoardValidators
{
    public class UpdateBoardDtoValidator : AbstractValidator<UpdateBoardDto>
    {
        public UpdateBoardDtoValidator()
        {
            RuleFor(b => b.Name)
                .MinimumLength(3).WithMessage("A board neve legalább 3 karakter hosszú legyen!")
                .MaximumLength(120).WithMessage("A board neve maximum 120 karakter lehet!")
                .When(b => b.Name != null);

            RuleFor(b => b.Description)
                .MaximumLength(500).WithMessage("A leírás maximum 500 karakter lehet!")
                .When(b => b.Description != null);

            RuleFor(b => b.RowVersion)
                .GreaterThan(0u).WithMessage("Érvénytelen RowVersion!");
        }
    }
}
