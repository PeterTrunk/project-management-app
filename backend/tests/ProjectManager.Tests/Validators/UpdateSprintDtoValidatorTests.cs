using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Sprints;
using ProjectManager.API.Validators.SprintValidators;

namespace ProjectManager.Tests.Validators
{
    public class UpdateSprintDtoValidatorTests
    {
        private readonly UpdateSprintDtoValidator _validator = new();

        private static UpdateSprintDto Valid() => new() { Name = "Sprint 1", RowVersion = 42 };

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
        [InlineData("   ")]
        [InlineData("ab")]
        public void Name_EmptyOrTooShort_ShouldHaveError(string name)
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

        //Goal

        [Fact]
        public void Goal_Null_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.Goal = null;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Goal);
        }

        [Fact]
        public void Goal_Exactly500Chars_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.Goal = new string('a', 500);
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Goal);
        }

        [Fact]
        public void Goal_Exactly501Chars_ShouldHaveError()
        {
            var dto = Valid();
            dto.Goal = new string('a', 501);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Goal);
        }

        //Dátumok

        [Fact]
        public void EndDate_BeforeStartDate_ShouldHaveError()
        {
            var dto = Valid();
            dto.StartDate = DateTime.UtcNow.AddDays(5);
            dto.EndDate = DateTime.UtcNow.AddDays(1);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.EndDate);
        }

        [Fact]
        public void EndDate_SameAsStartDate_ShouldHaveError()
        {
            var date = DateTime.UtcNow.AddDays(1);
            var dto = Valid();
            dto.StartDate = date;
            dto.EndDate = date;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.EndDate);
        }

        [Fact]
        public void EndDate_AfterStartDate_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.StartDate = DateTime.UtcNow;
            dto.EndDate = DateTime.UtcNow.AddDays(14);
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.EndDate);
        }

        //A When feltétel mindkét dátumot megköveteli: fél párra nincs összehasonlítás
        [Fact]
        public void StartDate_NullWithEndDateSet_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.StartDate = null;
            dto.EndDate = DateTime.UtcNow.AddDays(14);
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.EndDate);
        }

        [Fact]
        public void BothDates_Null_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.StartDate = null;
            dto.EndDate = null;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.EndDate);
        }

        //State

        //Eltérés a létrehozástól: a sprint állapotát ez a DTO nem tartalmazza, mert az
        //állapotátmenetnek külön végpontja van (indítás, lezárás) - ott üzleti szabályok is futnak.

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
        public void OnlyRowVersion_ShouldNotHaveAnyErrors()
        {
            _validator.TestValidate(new UpdateSprintDto { RowVersion = 7 })
                .ShouldNotHaveAnyValidationErrors();
        }
    }
}
