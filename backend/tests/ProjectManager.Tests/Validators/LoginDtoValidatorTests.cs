using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Auth;
using ProjectManager.API.Validators.AuthValidators;

namespace ProjectManager.Tests.Validators
{
    public class LoginDtoValidatorTests
    {
        private readonly LoginDtoValidator _validator = new();

        private static LoginDto Valid() => new() { Email = "user@example.com", Password = "Titok123!" };

        //Email

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Email_Empty_ShouldHaveError(string email)
        {
            var dto = Valid();
            dto.Email = email;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Email);
        }

        [Theory]
        [InlineData("nincs-kukac")]
        [InlineData("user@")]
        [InlineData("@example.com")]
        [InlineData("user example.com")]
        public void Email_InvalidFormat_ShouldHaveError(string email)
        {
            var dto = Valid();
            dto.Email = email;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Email);
        }

        [Theory]
        [InlineData("user@example.com")]
        [InlineData("first.last+tag@sub.example.co.uk")]
        public void Email_Valid_ShouldNotHaveError(string email)
        {
            var dto = Valid();
            dto.Email = email;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Email);
        }

        //Password

        [Fact]
        public void Password_Empty_ShouldHaveError()
        {
            var dto = Valid();
            dto.Password = "";
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Password);
        }

        //Bejelentkezésnél a jelszó erőssége már nem számít: azok a szabályok a regisztrációra
        //vonatkoznak. Egy régi, gyenge jelszóval rendelkező felhasználót nem zárhatunk ki.
        [Fact]
        public void Password_Weak_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.Password = "abc";
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Password);
        }

        [Fact]
        public void ValidDto_ShouldNotHaveAnyErrors()
        {
            _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
        }
    }
}
