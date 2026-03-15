using FluentValidation;
using ProjectManager.API.DTOs.Boards;

namespace ProjectManager.API.Validators.BoardValidators
{
    public class UpdateBoardDtoValidator : AbstractValidator<UpdateBoardDto>
    {
        public UpdateBoardDtoValidator()
        {
            RuleFor(b => b.Name)
                .MaximumLength(120)
                .MinimumLength(3)
                .When(b => b.Name !=null);

            RuleFor(b => b.Description)
                .MaximumLength(500)
                .When(b => b.Description != null);
        }
    }
}
