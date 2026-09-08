using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Auth;
using ProjectManager.API.Validators.AuthValidators;

namespace ProjectManager.Tests.Validators
{
    public class ResendVerificationDtoValidatorTests
    {
        private readonly ResendVerificationDtoValidator _validator = new();

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Email_Empty_ShouldHaveError(string email)
        {
            _validator.TestValidate(new ResendVerificationDto { Email = email })
                .ShouldHaveValidationErrorFor(x => x.Email);
        }

        [Theory]
        [InlineData("nincs-kukac")]
        [InlineData("user@")]
        [InlineData("@example.com")]
        public void Email_InvalidFormat_ShouldHaveError(string email)
        {
            _validator.TestValidate(new ResendVerificationDto { Email = email })
                .ShouldHaveValidationErrorFor(x => x.Email);
        }

        [Fact]
        public void Email_Valid_ShouldNotHaveError()
        {
            _validator.TestValidate(new ResendVerificationDto { Email = "user@example.com" })
                .ShouldNotHaveAnyValidationErrors();
        }
    }
}
