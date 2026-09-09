using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Auth;
using ProjectManager.API.Validators.AuthValidators;

namespace ProjectManager.Tests.Validators
{
    public class RegisterDtoValidatorTests
    {
        private readonly RegisterDtoValidator _validator = new();

        private static RegisterDto Valid() => new()
        {
            Email = "user@example.com",
            DisplayName = "Teszt Elek",
            Password = "Titok123!",
            AcceptedTerms = true,
            AcceptedTermsVersion = "2026-09-09"
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
        [InlineData("@example.com")]
        public void Email_InvalidFormat_ShouldHaveError(string email)
        {
            var dto = Valid();
            dto.Email = email;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Email);
        }

        [Fact]
        public void Email_Exactly254Chars_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.Email = new string('a', 242) + "@example.com";
            Assert.Equal(254, dto.Email.Length);
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Email);
        }

        [Fact]
        public void Email_Exactly255Chars_ShouldHaveError()
        {
            var dto = Valid();
            dto.Email = new string('a', 243) + "@example.com";
            Assert.Equal(255, dto.Email.Length);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Email);
        }

        //DisplayName

        [Fact]
        public void DisplayName_Empty_ShouldHaveError()
        {
            var dto = Valid();
            dto.DisplayName = "";
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.DisplayName);
        }

        [Fact]
        public void DisplayName_Exactly2Chars_ShouldHaveError()
        {
            var dto = Valid();
            dto.DisplayName = "ab";
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.DisplayName);
        }

        [Fact]
        public void DisplayName_Exactly3Chars_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.DisplayName = "abc";
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.DisplayName);
        }

        [Fact]
        public void DisplayName_Exactly120Chars_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.DisplayName = new string('a', 120);
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.DisplayName);
        }

        [Fact]
        public void DisplayName_Exactly121Chars_ShouldHaveError()
        {
            var dto = Valid();
            dto.DisplayName = new string('a', 121);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.DisplayName);
        }

        //A megjelenítendő név activity-leírásokba interpolálódik, ezért a markup-karakterek
        //mélységi védelemként tiltottak. Az elsődleges védelem a Svelte kimeneti escape-elése.
        [Theory]
        [InlineData("<script>alert(1)</script>")]
        [InlineData("Teszt <b>Elek</b>")]
        [InlineData("Tom & Jerry")]
        [InlineData("O\"Brien")]
        [InlineData("O'Brien")]
        [InlineData("back`tick")]
        public void DisplayName_ContainsMarkupCharacters_ShouldHaveError(string name)
        {
            var dto = Valid();
            dto.DisplayName = name;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.DisplayName);
        }

        [Theory]
        [InlineData("Teszt Elek")]
        [InlineData("Nagy-Kovács Anna")]
        [InlineData("José Ramírez")]
        [InlineData("user_123")]
        public void DisplayName_Valid_ShouldNotHaveError(string name)
        {
            var dto = Valid();
            dto.DisplayName = name;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.DisplayName);
        }

        //Password

        [Fact]
        public void Password_Empty_ShouldHaveError()
        {
            var dto = Valid();
            dto.Password = "";
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Password);
        }

        [Theory]
        [InlineData("Ab1!def", "hét karakteres")]
        [InlineData("abcdefg1!", "nincs benne nagybetű")]
        [InlineData("Abcdefgh!", "nincs benne szám")]
        [InlineData("Abcdefg1", "nincs benne speciális karakter")]
        public void Password_MissingRequirement_ShouldHaveError(string password, string reason)
        {
            var dto = Valid();
            dto.Password = password;

            var result = _validator.TestValidate(dto);

            Assert.True(
                result.Errors.Any(e => e.PropertyName == nameof(RegisterDto.Password)),
                $"A jelszó átment, pedig: {reason}");
        }

        [Fact]
        public void Password_Exactly8CharsWithEveryRequirement_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.Password = "Abcdef1!";
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Password);
        }

        [Theory]
        [InlineData("!")]
        [InlineData("@")]
        [InlineData("#")]
        [InlineData("$")]
        [InlineData("%")]
        [InlineData("^")]
        [InlineData("&")]
        [InlineData("*")]
        public void Password_EachAcceptedSpecialCharacter_ShouldNotHaveError(string special)
        {
            var dto = Valid();
            dto.Password = "Abcdefg1" + special;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Password);
        }

        //A szabály csak a felsorolt nyolc karaktert fogadja el speciálisként
        [Theory]
        [InlineData("-")]
        [InlineData("_")]
        [InlineData("+")]
        [InlineData("?")]
        public void Password_UnlistedSpecialCharacter_ShouldHaveError(string special)
        {
            var dto = Valid();
            dto.Password = "Abcdefg1" + special;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Password);
        }

        //Feltételek elfogadása

        //A felület letiltja a gombot pipa nélkül, de az API nyilvános: egy közvetlen kérés
        //megkerülné a jelölőnégyzetet, ezért a szerveroldali kikényszerítés a lényegi védelem.
        [Fact]
        public void AcceptedTerms_False_ShouldHaveError()
        {
            var dto = Valid();
            dto.AcceptedTerms = false;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.AcceptedTerms);
        }

        [Fact]
        public void AcceptedTerms_Missing_ShouldHaveError()
        {
            //A bool alapértéke false: a mezőt kihagyó kérés is elutasításra kerül
            var dto = new RegisterDto
            {
                Email = "user@example.com",
                DisplayName = "Teszt Elek",
                Password = "Titok123!",
                AcceptedTermsVersion = "2026-09-09"
            };
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.AcceptedTerms);
        }

        [Fact]
        public void AcceptedTerms_True_ShouldNotHaveError()
        {
            _validator.TestValidate(Valid()).ShouldNotHaveValidationErrorFor(x => x.AcceptedTerms);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void AcceptedTermsVersion_Empty_ShouldHaveError(string version)
        {
            var dto = Valid();
            dto.AcceptedTermsVersion = version;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.AcceptedTermsVersion);
        }

        //A verzió egyezőségét szándékosan nem itt mérjük: ahhoz az adatbázisban tárolt
        //hatályos verzió kell, ezért az az AuthService felelőssége.
        [Fact]
        public void AcceptedTermsVersion_AnyNonEmptyValue_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.AcceptedTermsVersion = "1999-01-01";
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.AcceptedTermsVersion);
        }

        [Fact]
        public void ValidDto_ShouldNotHaveAnyErrors()
        {
            _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
        }
    }
}
