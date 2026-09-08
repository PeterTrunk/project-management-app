using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Auth;
using ProjectManager.API.Validators.AuthValidators;

namespace ProjectManager.Tests.Validators
{
    public class RefreshTokenDtoValidatorTests
    {
        private readonly RefreshTokenDtoValidator _validator = new();

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void RefreshToken_Empty_ShouldHaveError(string token)
        {
            _validator.TestValidate(new RefreshTokenDto { RefreshToken = token })
                .ShouldHaveValidationErrorFor(x => x.RefreshToken);
        }

        //A tokent a szolgáltatás hasheli és keresi az adatbázisban, a formájára nincs szabály
        [Fact]
        public void RefreshToken_NonEmpty_ShouldNotHaveError()
        {
            _validator.TestValidate(new RefreshTokenDto { RefreshToken = "eGvT7-9kQm_2sLpR4wXyZaB1cD3eF5gH" })
                .ShouldNotHaveAnyValidationErrors();
        }
    }
}
