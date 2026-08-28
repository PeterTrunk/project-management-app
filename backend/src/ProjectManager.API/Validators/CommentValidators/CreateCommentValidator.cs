using FluentValidation;
using ProjectManager.API.DTOs.Comments;

namespace ProjectManager.API.Validators.CommentValidators
{
    public class CreateCommentDtoValidator : AbstractValidator<CreateCommentDto>
    {
        public CreateCommentDtoValidator()
        {
            RuleFor(x => x.Body)
                .NotEmpty().WithMessage("A komment tartalma kötelező!")
                .MaximumLength(2000).WithMessage("A komment maximum 2000 karakter lehet!");
        }
    }
}
