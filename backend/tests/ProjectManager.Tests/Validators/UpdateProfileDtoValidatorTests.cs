using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Auth;
using ProjectManager.API.Validators.AuthValidators;

namespace ProjectManager.Tests.Validators
{
    public class UpdateProfileDtoValidatorTests
    {
        private readonly UpdateProfileDtoValidator _validator = new();

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void DisplayName_Empty_ShouldHaveError(string name)
        {
            _validator.TestValidate(new UpdateProfileDto { DisplayName = name })
                .ShouldHaveValidationErrorFor(x => x.DisplayName);
        }

        [Fact]
        public void DisplayName_Exactly2Chars_ShouldHaveError()
        {
            _validator.TestValidate(new UpdateProfileDto { DisplayName = "ab" })
                .ShouldHaveValidationErrorFor(x => x.DisplayName);
        }

        [Fact]
        public void DisplayName_Exactly3Chars_ShouldNotHaveError()
        {
            _validator.TestValidate(new UpdateProfileDto { DisplayName = "abc" })
                .ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void DisplayName_Exactly120Chars_ShouldNotHaveError()
        {
            _validator.TestValidate(new UpdateProfileDto { DisplayName = new string('a', 120) })
                .ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void DisplayName_Exactly121Chars_ShouldHaveError()
        {
            _validator.TestValidate(new UpdateProfileDto { DisplayName = new string('a', 121) })
                .ShouldHaveValidationErrorFor(x => x.DisplayName);
        }

        //Ugyanaz a markup-tiltás, mint a regisztrációnál: a profil később is átírható lenne,
        //ha csak a RegisterDtoValidator zárná ki a jelölőkaraktereket.
        [Theory]
        [InlineData("<script>alert(1)</script>")]
        [InlineData("Teszt <b>Elek</b>")]
        [InlineData("Tom & Jerry")]
        [InlineData("O\"Brien")]
        [InlineData("O'Brien")]
        [InlineData("back`tick")]
        public void DisplayName_ContainsMarkupCharacters_ShouldHaveError(string name)
        {
            _validator.TestValidate(new UpdateProfileDto { DisplayName = name })
                .ShouldHaveValidationErrorFor(x => x.DisplayName);
        }

        [Theory]
        [InlineData("Teszt Elek")]
        [InlineData("Nagy-Kovács Anna")]
        [InlineData("José Ramírez")]
        [InlineData("user_123")]
        public void DisplayName_Valid_ShouldNotHaveError(string name)
        {
            _validator.TestValidate(new UpdateProfileDto { DisplayName = name })
                .ShouldNotHaveAnyValidationErrors();
        }
    }
}
