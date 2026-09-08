using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Columns;
using ProjectManager.API.Validators.ColumnValidators;

namespace ProjectManager.Tests.Validators
{
    public class ColumnOrderDtoValidatorTests
    {
        private readonly ColumnOrderDtoValidator _validator = new();

        private static ColumnOrderDto Valid() => new()
        {
            Id = Guid.NewGuid(),
            Position = 1,
            RowVersion = 42
        };

        //Id

        //A NotEmpty Guidra az üres Guidot jelenti: a JSON-ból hiányzó azonosító így nem
        //csúszhat át nulla Guidként az adatbázis-lekérdezésbe.
        [Fact]
        public void Id_EmptyGuid_ShouldHaveError()
        {
            var dto = Valid();
            dto.Id = Guid.Empty;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Id);
        }

        [Fact]
        public void Id_RealGuid_ShouldNotHaveError()
        {
            _validator.TestValidate(Valid()).ShouldNotHaveValidationErrorFor(x => x.Id);
        }

        //Position

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
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

        //RowVersion

        [Fact]
        public void RowVersion_Zero_ShouldHaveError()
        {
            var dto = Valid();
            dto.RowVersion = 0;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.RowVersion);
        }

        [Fact]
        public void RowVersion_Positive_ShouldNotHaveError()
        {
            _validator.TestValidate(Valid()).ShouldNotHaveValidationErrorFor(x => x.RowVersion);
        }

        [Fact]
        public void ValidDto_ShouldNotHaveAnyErrors()
        {
            _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
        }
    }
}
