using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Auth;
using ProjectManager.API.Validators.AuthValidators;

namespace ProjectManager.Tests.Validators
{
    public class ChangePasswordDtoValidatorTests
    {
        private readonly ChangePasswordDtoValidator _validator = new();

        private static ChangePasswordDto Valid() => new()
        {
            CurrentPassword = "Regi123!",
            NewPassword = "Uj123456!"
        };

        //CurrentPassword

        [Fact]
        public void CurrentPassword_Empty_ShouldHaveError()
        {
            var dto = Valid();
            dto.CurrentPassword = "";
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.CurrentPassword);
        }

        //A jelenlegi jelszóra nincs erősségi szabály: az a mai értéket adja meg, nem újat.
        [Fact]
        public void CurrentPassword_Weak_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.CurrentPassword = "abc";
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.CurrentPassword);
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

        //A validátor nem tiltja, hogy az új jelszó megegyezzen a régivel - ezt a szolgáltatás dönti el
        [Fact]
        public void NewPassword_SameAsCurrent_ShouldNotHaveError()
        {
            var dto = new ChangePasswordDto { CurrentPassword = "Abcdef1!", NewPassword = "Abcdef1!" };
            _validator.TestValidate(dto).ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void ValidDto_ShouldNotHaveAnyErrors()
        {
            _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
        }
    }
}
