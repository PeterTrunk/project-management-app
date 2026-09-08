using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Auth;
using ProjectManager.API.Validators.AuthValidators;

namespace ProjectManager.Tests.Validators
{
    public class DisableTotpDtoValidatorTests
    {
        private readonly DisableTotpDtoValidator _validator = new();

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void CurrentPassword_Empty_ShouldHaveError(string password)
        {
            _validator.TestValidate(new DisableTotpDto { CurrentPassword = password })
                .ShouldHaveValidationErrorFor(x => x.CurrentPassword);
        }

        //A kétlépcsős azonosítás kikapcsolásához a meglévő jelszó kell; annak erősségét
        //itt nem mérjük, csak a jelenlétét - az ellenőrzés a hash összevetése.
        [Theory]
        [InlineData("abc")]
        [InlineData("Titok123!")]
        public void CurrentPassword_AnyNonEmptyValue_ShouldNotHaveError(string password)
        {
            _validator.TestValidate(new DisableTotpDto { CurrentPassword = password })
                .ShouldNotHaveAnyValidationErrors();
        }
    }
}
