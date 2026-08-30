using FluentValidation;
using ProjectManager.API.DTOs.Attachment;

namespace ProjectManager.API.Validators.AttachmentValidators
{
    public class PresignedUrlRequestDtoValidator : AbstractValidator<PresignedUrlRequestDto>
    {
        public PresignedUrlRequestDtoValidator()
        {
            RuleFor(d => d.FileName)
                .NotEmpty().WithMessage("A fájlnév megadása kötelező!")
                .MaximumLength(255).WithMessage("A fájlnév maximum 255 karakter lehet!");

            RuleFor(d => d.ContentType)
                .NotEmpty().WithMessage("A fájl típusának megadása kötelező!")
                .MaximumLength(120).WithMessage("A content type maximum 120 karakter lehet!");

            RuleFor(d => d.SizeBytes)
                .GreaterThan(0).WithMessage("A fájl mérete nem lehet nulla!");
        }
    }
}
