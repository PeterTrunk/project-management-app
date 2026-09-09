using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Auth;
using ProjectManager.API.Validators.AuthValidators;

namespace ProjectManager.Tests.Validators
{
    public class DeleteAccountDtoValidatorTests
    {
        private readonly DeleteAccountDtoValidator _validator = new();

        private static DeleteAccountDto Valid() => new() { CurrentPassword = "Titok123!" };

        //CurrentPassword

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void CurrentPassword_Empty_ShouldHaveError(string password)
        {
            var dto = Valid();
            dto.CurrentPassword = password;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.CurrentPassword);
        }

        //A meglévő jelszó erősségét nem mérjük, csak a jelenlétét: az ellenőrzés a hash összevetése
        [Fact]
        public void CurrentPassword_AnyNonEmptyValue_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.CurrentPassword = "abc";
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.CurrentPassword);
        }

        //TotpToken

        //A null azt jelenti, hogy a fiókon nincs kétfaktoros hitelesítés. Hogy KELL-E kód,
        //azt a szolgáltatás dönti el, mert ahhoz a felhasználó állapota kell.
        [Fact]
        public void TotpToken_Null_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.TotpToken = null;
            _validator.TestValidate(dto).ShouldNotHaveAnyValidationErrors();
        }

        [Theory]
        [InlineData("")]
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
            _validator.TestValidate(dto).ShouldNotHaveAnyValidationErrors();
        }
    }
}
