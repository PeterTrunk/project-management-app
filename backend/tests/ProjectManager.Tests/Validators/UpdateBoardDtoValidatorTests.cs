using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Boards;
using ProjectManager.API.Validators.BoardValidators;

namespace ProjectManager.Tests.Validators
{
    /// <summary>
    /// Részleges frissítés: a null mezők azt jelentik, hogy "ne változtasd", ezért rájuk
    /// nem szabad hosszszabályt futtatni. A RowVersion viszont mindig kötelező.
    /// </summary>
    public class UpdateBoardDtoValidatorTests
    {
        private readonly UpdateBoardDtoValidator _validator = new();

        private static UpdateBoardDto Valid() => new() { Name = "Fejlesztés", RowVersion = 42 };

        //Name

        [Fact]
        public void Name_Null_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.Name = null;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Name);
        }

        //A When(Name != null) miatt az üres sztring NEM null, tehát a MinimumLength megfogja
        [Fact]
        public void Name_Empty_ShouldHaveError()
        {
            var dto = Valid();
            dto.Name = "";
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Name_Exactly2Chars_ShouldHaveError()
        {
            var dto = Valid();
            dto.Name = "ab";
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Name_Exactly3Chars_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.Name = "abc";
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Name_Exactly120Chars_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.Name = new string('a', 120);
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Name_Exactly121Chars_ShouldHaveError()
        {
            var dto = Valid();
            dto.Name = new string('a', 121);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Name);
        }

        //Description

        [Fact]
        public void Description_Null_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.Description = null;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Description);
        }

        [Fact]
        public void Description_Empty_ShouldNotHaveError()
        {
            //Az üres sztring itt szándékos törlés, nem hiba
            var dto = Valid();
            dto.Description = "";
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Description);
        }

        [Fact]
        public void Description_Exactly500Chars_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.Description = new string('a', 500);
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Description);
        }

        [Fact]
        public void Description_Exactly501Chars_ShouldHaveError()
        {
            var dto = Valid();
            dto.Description = new string('a', 501);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Description);
        }

        //RowVersion

        //A 0 az alapértelmezett uint érték: ha a kliens elfelejti elküldeni, az optimistic
        //concurrency ellenőrzés némán kimaradna - ezért kötelezően pozitív.
        [Fact]
        public void RowVersion_Zero_ShouldHaveError()
        {
            var dto = Valid();
            dto.RowVersion = 0;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.RowVersion);
        }

        [Theory]
        [InlineData(1u)]
        [InlineData(uint.MaxValue)]
        public void RowVersion_Positive_ShouldNotHaveError(uint rowVersion)
        {
            var dto = Valid();
            dto.RowVersion = rowVersion;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.RowVersion);
        }

        [Fact]
        public void AllFieldsNullButRowVersion_ShouldNotHaveAnyErrors()
        {
            //A "csak az IsDefault kapcsolót állítom" eset
            var dto = new UpdateBoardDto { IsDefault = true, RowVersion = 7 };
            _validator.TestValidate(dto).ShouldNotHaveAnyValidationErrors();
        }
    }
}
