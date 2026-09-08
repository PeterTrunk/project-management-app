using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Auth;
using ProjectManager.API.Validators.AuthValidators;

namespace ProjectManager.Tests.Validators
{
    public class ForgotPasswordDtoValidatorTests
    {
        private readonly ForgotPasswordDtoValidator _validator = new();

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Email_Empty_ShouldHaveError(string email)
        {
            _validator.TestValidate(new ForgotPasswordDto { Email = email })
                .ShouldHaveValidationErrorFor(x => x.Email);
        }

        [Theory]
        [InlineData("nincs-kukac")]
        [InlineData("user@")]
        [InlineData("@example.com")]
        [InlineData("user example.com")]
        public void Email_InvalidFormat_ShouldHaveError(string email)
        {
            _validator.TestValidate(new ForgotPasswordDto { Email = email })
                .ShouldHaveValidationErrorFor(x => x.Email);
        }

        [Theory]
        [InlineData("user@example.com")]
        [InlineData("first.last+tag@sub.example.co.uk")]
        public void Email_Valid_ShouldNotHaveError(string email)
        {
            _validator.TestValidate(new ForgotPasswordDto { Email = email })
                .ShouldNotHaveAnyValidationErrors();
        }
    }
}
