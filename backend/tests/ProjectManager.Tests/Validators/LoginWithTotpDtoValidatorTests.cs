using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Auth;
using ProjectManager.API.Validators.AuthValidators;

namespace ProjectManager.Tests.Validators
{
    public class LoginWithTotpDtoValidatorTests
    {
        private readonly LoginWithTotpDtoValidator _validator = new();

        private static LoginWithTotpDto Valid() => new()
        {
            Email = "user@example.com",
            Password = "Titok123!",
            TotpToken = "123456"
        };

        //Email

        [Fact]
        public void Email_Empty_ShouldHaveError()
        {
            var dto = Valid();
            dto.Email = "";
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Email);
        }

        [Theory]
        [InlineData("nincs-kukac")]
        [InlineData("user@")]
        public void Email_InvalidFormat_ShouldHaveError(string email)
        {
            var dto = Valid();
            dto.Email = email;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Email);
        }

        //Password

        [Fact]
        public void Password_Empty_ShouldHaveError()
        {
            var dto = Valid();
            dto.Password = "";
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Password);
        }

        //TotpToken

        [Fact]
        public void TotpToken_Empty_ShouldHaveError()
        {
            var dto = Valid();
            dto.TotpToken = "";
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.TotpToken);
        }

        [Theory]
        [InlineData("12345")]
        [InlineData("1234567")]
        public void TotpToken_WrongLength_ShouldHaveError(string token)
        {
            var dto = Valid();
            dto.TotpToken = token;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.TotpToken);
        }

        [Theory]
        [InlineData("12345a")]
        [InlineData("abcdef")]
        [InlineData("12-456")]
        public void TotpToken_NotOnlyDigits_ShouldHaveError(string token)
        {
            var dto = Valid();
            dto.TotpToken = token;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.TotpToken);
        }

        [Theory]
        [InlineData("123456")]
        [InlineData("000000")]
        public void TotpToken_SixDigits_ShouldNotHaveError(string token)
        {
            var dto = Valid();
            dto.TotpToken = token;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.TotpToken);
        }

        [Fact]
        public void ValidDto_ShouldNotHaveAnyErrors()
        {
            _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
        }
    }
}
