using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Columns;
using ProjectManager.API.Validators.ColumnValidators;

namespace ProjectManager.Tests.Validators
{
    public class CreateColumnDtoValidatorTests
    {
        private readonly CreateColumnDtoValidator _validator = new();

        private static CreateColumnDto Valid() => new()
        {
            BoardId = Guid.NewGuid(),
            Name = "Folyamatban",
            MapsToStatus = "InProgress",
            Position = 1
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
        public void MapsToStatus_Empty_ShouldHaveError()
        {
            var dto = Valid();
            dto.MapsToStatus = "";
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.MapsToStatus);
        }

        [Fact]
        public void MapsToStatus_Exactly2Chars_ShouldHaveError()
        {
            var dto = Valid();
            dto.MapsToStatus = "ab";
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.MapsToStatus);
        }

        [Fact]
        public void MapsToStatus_Exactly3Chars_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.MapsToStatus = "abc";
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.MapsToStatus);
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

        //Position

        //A 0-ás pozíció a Backlog oszlopé: ha egy létrehozott oszlop elfoglalná,
        //két oszlop kerülne ugyanarra a helyre.
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public void Position_NotPositive_ShouldHaveError(int position)
        {
            var dto = Valid();
            dto.Position = position;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Position);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(99)]
        public void Position_Positive_ShouldNotHaveError(int position)
        {
            var dto = Valid();
            dto.Position = position;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Position);
        }

        //WipLimit

        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        [InlineData(5)]
        public void WipLimit_AnyValue_ShouldNotHaveError(int? wipLimit)
        {
            //A WIP limitre ma nincs szabály - ha valaha lesz, ez a teszt bukik el elsőként
            var dto = Valid();
            dto.WipLimit = wipLimit;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.WipLimit);
        }

        [Fact]
        public void ValidDto_ShouldNotHaveAnyErrors()
        {
            _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
        }
    }
}
