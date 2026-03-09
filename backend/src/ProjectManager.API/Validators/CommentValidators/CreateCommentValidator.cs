using FluentValidation;
using ProjectManager.API.DTOs.Comments;

namespace ProjectManager.API.Validators.CommentValidators
{
    public class CreateCommentDtoValidator : AbstractValidator<CreateCommentDto>
    {
        public CreateCommentDtoValidator()
        {
            RuleFor(x => x.Body)
                .NotEmpty()
                .MaximumLength(2000);
        }
    }
}
