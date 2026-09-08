using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Auth;
using ProjectManager.API.Validators.AuthValidators;

namespace ProjectManager.Tests.Validators
{
    public class ResetPasswordDtoValidatorTests
    {
        private readonly ResetPasswordDtoValidator _validator = new();

        private static ResetPasswordDto Valid() => new()
        {
            Token = "eGvT7-9kQm_2sLpR4wXyZaB1cD3eF5gH",
            NewPassword = "Uj123456!"
        };

        //Token

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Token_Empty_ShouldHaveError(string token)
        {
            var dto = Valid();
            dto.Token = token;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Token);
        }

        //A token alakját szándékosan nem méri a validátor: az érvényességet az adatbázisban
        //tárolt hash és a lejárat dönti el, nem egy formai szabály.
        [Fact]
        public void Token_ArbitraryString_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.Token = "barmi";
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Token);
        }

        //NewPassword

        [Fact]
        public void NewPassword_Empty_ShouldHaveError()
        {
            var dto = Valid();
            dto.NewPassword = "";
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.NewPassword);
        }

        [Theory]
        [InlineData("Ab1!def")]
        [InlineData("abcdefg1!")]
        [InlineData("Abcdefgh!")]
        [InlineData("Abcdefg1")]
        public void NewPassword_MissingRequirement_ShouldHaveError(string password)
        {
            var dto = Valid();
            dto.NewPassword = password;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.NewPassword);
        }

        [Fact]
        public void NewPassword_Exactly8CharsWithEveryRequirement_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.NewPassword = "Abcdef1!";
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.NewPassword);
        }

        [Fact]
        public void ValidDto_ShouldNotHaveAnyErrors()
        {
            _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
        }
    }
}
