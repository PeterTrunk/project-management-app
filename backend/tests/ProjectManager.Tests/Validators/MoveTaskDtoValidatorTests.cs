using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.ProjectTask;

namespace ProjectManager.Tests.Validators
{
    public class MoveTaskDtoValidatorTests
    {
        private readonly MoveTaskDtoValidator _validator = new();

        private static MoveTaskDto Valid() => new() { ColumnId = Guid.NewGuid(), RowVersion = 42 };

        //ColumnId

        [Fact]
        public void ColumnId_Null_ShouldHaveError()
        {
            var dto = Valid();
            dto.ColumnId = null;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.ColumnId);
        }

        //A ColumnId típusa Guid?, ezért a NotEmpty a default(Guid?)-hoz, vagyis a nullhoz
        //hasonlít - a csupa nullás Guid átmegy rajta. (A ColumnOrderDto.Id nem nullozható
        //Guid, ott ugyanez a szabály helyesen fogja meg az üres azonosítót.)
        //Következmény: az ilyen kérés nem 400-zal, hanem a szolgáltatás 404-esével áll meg.
        [Fact]
        public void ColumnId_EmptyGuid_PassesValidation()
        {
            var dto = Valid();
            dto.ColumnId = Guid.Empty;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.ColumnId);
        }

        [Fact]
        public void ColumnId_RealGuid_ShouldNotHaveError()
        {
            _validator.TestValidate(Valid()).ShouldNotHaveValidationErrorFor(x => x.ColumnId);
        }

        //AfterTaskId

        //Szándékosan nincs rá szabály: a null az "első helyre" jelentése, a nem null értéket
        //pedig a szolgáltatás ellenőrzi - létező, ugyanabban az oszlopban lévő taskra kell mutasson.
        [Fact]
        public void AfterTaskId_Null_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.AfterTaskId = null;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.AfterTaskId);
        }

        [Fact]
        public void AfterTaskId_EmptyGuid_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.AfterTaskId = Guid.Empty;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.AfterTaskId);
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

        [Fact]
        public void ValidDto_ShouldNotHaveAnyErrors()
        {
            _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
        }
    }
}
