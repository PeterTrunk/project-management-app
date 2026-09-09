using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Auth;
using ProjectManager.API.Validators.AuthValidators;

namespace ProjectManager.Tests.Validators
{
    public class VerifyTotpDtoValidatorTests
    {
        private readonly VerifyTotpDtoValidator _validator = new();

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Token_Empty_ShouldHaveError(string token)
        {
            _validator.TestValidate(new VerifyTotpDto { Token = token })
                .ShouldHaveValidationErrorFor(x => x.Token);
        }

        [Theory]
        [InlineData("12345")]
        [InlineData("1234567")]
        public void Token_WrongLength_ShouldHaveError(string token)
        {
            _validator.TestValidate(new VerifyTotpDto { Token = token })
                .ShouldHaveValidationErrorFor(x => x.Token);
        }

        [Theory]
        [InlineData("12345a")]
        [InlineData("abcdef")]
        [InlineData("12 456")]
        [InlineData("+12345")]
        public void Token_NotOnlyDigits_ShouldHaveError(string token)
        {
            _validator.TestValidate(new VerifyTotpDto { Token = token })
                .ShouldHaveValidationErrorFor(x => x.Token);
        }

        [Theory]
        [InlineData("123456")]
        [InlineData("000000")]
        [InlineData("999999")]
        public void Token_SixDigits_ShouldNotHaveError(string token)
        {
            //A vezető nullás kód is érvényes: a TOTP szöveg, nem szám
            _validator.TestValidate(new VerifyTotpDto { Token = token })
                .ShouldNotHaveAnyValidationErrors();
        }
    }
}
