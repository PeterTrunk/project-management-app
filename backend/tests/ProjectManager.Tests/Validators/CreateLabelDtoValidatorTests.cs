using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Labels;
using ProjectManager.API.Validators.LabelValidators;

namespace ProjectManager.Tests.Validators
{
    public class CreateLabelDtoValidatorTests
    {
        private readonly CreateLabelDtoValidator _validator = new();

        private static CreateLabelDto Valid() => new() { Name = "bug", Color = "#FF0000" };

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
        public void Name_Exactly40Chars_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.Name = new string('a', 40);
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Name_Exactly41Chars_ShouldHaveError()
        {
            var dto = Valid();
            dto.Name = new string('a', 41);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Name);
        }

        //Color

        [Fact]
        public void Color_Empty_ShouldHaveError()
        {
            var dto = Valid();
            dto.Color = "";
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Color);
        }

        //A színt a frontend közvetlenül a stílusba írja, ezért a formátumnak szigorúnak kell lennie
        [Theory]
        [InlineData("FF0000")]
        [InlineData("#FF000")]
        [InlineData("#FF00000")]
        [InlineData("#GG0000")]
        [InlineData("#F00")]
        [InlineData("red")]
        [InlineData("rgb(255,0,0)")]
        [InlineData("#FF0000;background:url(x)")]
        public void Color_InvalidHexFormat_ShouldHaveError(string color)
        {
            var dto = Valid();
            dto.Color = color;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Color);
        }

        [Theory]
        [InlineData("#FF0000")]
        [InlineData("#ff0000")]
        [InlineData("#AbCdEf")]
        [InlineData("#000000")]
        [InlineData("#FFFFFF")]
        public void Color_ValidHexFormat_ShouldNotHaveError(string color)
        {
            var dto = Valid();
            dto.Color = color;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Color);
        }

        [Fact]
        public void ValidDto_ShouldNotHaveAnyErrors()
        {
            _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
        }
    }
}
