using FluentValidation;
using ProjectManager.API.DTOs.Boards;

namespace ProjectManager.API.Validators.BoardValidators
{
    public class CreateBoardDtoValidator : AbstractValidator<CreateBoardDto>
    {
        public CreateBoardDtoValidator()
        {
            RuleFor(b => b.Name)
                .NotEmpty()
                .MaximumLength(120)
                .MinimumLength(3);

            RuleFor(b => b.Description)
                .MaximumLength(500);
        }
    }
}
