using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Columns;
using ProjectManager.API.Validators.ColumnValidators;

namespace ProjectManager.Tests.Validators
{
    public class UpdateColumnDtoValidatorTests
    {
        private readonly UpdateColumnDtoValidator _validator = new();

        private static UpdateColumnDto Valid() => new() { Name = "Folyamatban", RowVersion = 42 };

        //Name

        [Fact]
        public void Name_Null_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.Name = null;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Name);
        }

        [Theory]
        [InlineData("")]
        [InlineData("ab")]
        public void Name_TooShort_ShouldHaveError(string name)
        {
            var dto = Valid();
            dto.Name = name;
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
        public void Name_Exactly80Chars_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.Name = new string('a', 80);
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Name_Exactly81Chars_ShouldHaveError()
        {
            var dto = Valid();
            dto.Name = new string('a', 81);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Name);
        }

        //MapsToStatus

        [Fact]
        public void MapsToStatus_Null_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.MapsToStatus = null;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.MapsToStatus);
        }

        [Theory]
        [InlineData("")]
        [InlineData("ab")]
        public void MapsToStatus_TooShort_ShouldHaveError(string status)
        {
            var dto = Valid();
            dto.MapsToStatus = status;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.MapsToStatus);
        }

        [Fact]
        public void MapsToStatus_Exactly32Chars_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.MapsToStatus = new string('a', 32);
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.MapsToStatus);
        }

        [Fact]
        public void MapsToStatus_Exactly33Chars_ShouldHaveError()
        {
            var dto = Valid();
            dto.MapsToStatus = new string('a', 33);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.MapsToStatus);
        }

        //RowVersion

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

        //A pozíciót ez a DTO nem tartalmazza: az átrendezés a ColumnOrderDto dolga
        [Fact]
        public void OnlyRowVersion_ShouldNotHaveAnyErrors()
        {
            _validator.TestValidate(new UpdateColumnDto { RowVersion = 7 })
                .ShouldNotHaveAnyValidationErrors();
        }
    }
}
