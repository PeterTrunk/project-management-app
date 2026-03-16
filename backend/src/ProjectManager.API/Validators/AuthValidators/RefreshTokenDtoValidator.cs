using FluentValidation;
using ProjectManager.API.DTOs.Auth;

namespace ProjectManager.API.Validators.AuthValidators
{
    public class RefreshTokenDtoValidator : AbstractValidator<RefreshTokenDto>
    {
        public RefreshTokenDtoValidator()
        {
            RuleFor(x => x.RefreshToken).NotEmpty();
        }
    }
}
