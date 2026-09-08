using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Boards;
using ProjectManager.API.Validators.BoardValidators;

namespace ProjectManager.Tests.Validators
{
    public class CreateBoardDtoValidatorTests
    {
        private readonly CreateBoardDtoValidator _validator = new();

        private static CreateBoardDto Valid() => new()
        {
            ProjectId = Guid.NewGuid(),
            Name = "Fejlesztés",
            Description = "A csapat fő boardja"
        };

        //Name

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Name_Empty_ShouldHaveError(string name)
        {
            var dto = Valid();
            dto.Name = name;
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
        public void Description_Empty_ShouldNotHaveError()
        {
            //A leírás nem kötelező, csak a hossza korlátozott
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

        [Fact]
        public void ValidDto_ShouldNotHaveAnyErrors()
        {
            _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
        }
    }
}
