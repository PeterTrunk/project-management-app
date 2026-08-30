using FluentValidation;
using ProjectManager.API.DTOs.Attachment;

namespace ProjectManager.API.Validators.AttachmentValidators
{
    public class ConfirmUploadDtoValidator : AbstractValidator<ConfirmUploadDto>
    {
        public ConfirmUploadDtoValidator()
        {
            RuleFor(d => d.StorageKey)
                .NotEmpty().WithMessage("A storage key megadása kötelező!");
        }
    }
}
