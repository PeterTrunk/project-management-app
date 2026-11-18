using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Integration;
using ProjectManager.API.Validators.IntegrationValidators;

namespace ProjectManager.Tests.Validators
{
    /// <summary>
    /// A hozzáférési token beállítása. A webhook titokkal ellentétben itt az ÜRES érték
    /// érvényes bemenet: az a token törlése, vagyis a visszavonás útja. Ezért nincs
    /// minimális hossz és nincs NotEmpty szabály sem.
    /// </summary>
    public class SetAccessTokenDtoValidatorTests
    {
        private readonly SetAccessTokenDtoValidator _validator = new();

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void AccessToken_Empty_ShouldNotHaveError(string? token)
        {
            _validator.TestValidate(new SetAccessTokenDto { AccessToken = token })
                .ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void AccessToken_Typical_ShouldNotHaveError()
        {
            _validator.TestValidate(new SetAccessTokenDto { AccessToken = "ghp_" + new string('a', 36) })
                .ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void AccessToken_Exactly500Chars_ShouldNotHaveError()
        {
            _validator.TestValidate(new SetAccessTokenDto { AccessToken = new string('a', 500) })
                .ShouldNotHaveAnyValidationErrors();
        }

        //A felső korlát a nyilvánvalóan hibás bemenet ellen van (beillesztett fájl, egész kulcs)
        [Fact]
        public void AccessToken_TooLong_ShouldHaveError()
        {
            _validator.TestValidate(new SetAccessTokenDto { AccessToken = new string('a', 501) })
                .ShouldHaveValidationErrorFor(x => x.AccessToken);
        }
    }
}
