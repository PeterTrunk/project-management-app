using FluentValidation.TestHelper;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.DTOs.Sprints;
using ProjectManager.API.Validators.SprintValidators;

namespace ProjectManager.Tests.Validators
{
    public class CreateSprintDtoValidatorTests
    {
        private readonly CreateSprintDtoValidator _validator = new();

        private static CreateSprintDto Valid() => new()
        {
            ProjectId = Guid.NewGuid(),
            Name = "Sprint 1",
            State = SprintStates.Planning
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

        //A felső határ 80 karakter, nem 120: a korábbi teszt 121 karakterrel próbálkozott,
        //ezért zöld volt anélkül, hogy a valódi határt mérte volna.
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

        [Fact]
        public void Name_Valid_ShouldNotHaveError()
        {
            _validator.TestValidate(Valid()).ShouldNotHaveValidationErrorFor(x => x.Name);
        }

        //State

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void State_Empty_ShouldHaveError(string state)
        {
            var dto = Valid();
            dto.State = state;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.State);
        }

        [Theory]
        [InlineData(SprintStates.Planning)]
        [InlineData(SprintStates.Active)]
        [InlineData(SprintStates.Completed)]
        public void State_KnownValue_ShouldNotHaveError(string state)
        {
            var dto = Valid();
            dto.State = state;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.State);
        }

        //A lista összehasonlítása kis- és nagybetűérzékeny
        [Theory]
        [InlineData("planning")]
        [InlineData("ACTIVE")]
        [InlineData("Planned")]
        [InlineData("Closed")]
        [InlineData("Archived")]
        public void State_UnknownValue_ShouldHaveError(string state)
        {
            var dto = Valid();
            dto.State = state;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.State);
        }

        //Ez a teszt akkor bukik el, ha a SprintStates bővül, de a validátor tesztjei nem követik
        [Fact]
        public void EveryDeclaredState_IsAccepted()
        {
            Assert.All(SprintStates.ValidStates, state =>
            {
                var dto = Valid();
                dto.State = state;
                _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.State);
            });
        }

        //Dátum logika

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

        [Fact]
        public void BothDates_Null_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.StartDate = null;
            dto.EndDate = null;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.EndDate);
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.StartDate);
        }

        //A When feltétel mindkét dátumot megköveteli: fél párra nincs összehasonlítás
        [Fact]
        public void StartDate_NullEndDateSet_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.StartDate = null;
            dto.EndDate = DateTime.UtcNow.AddDays(14);
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.EndDate);
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

        [Fact]
        public void ValidDto_ShouldNotHaveAnyErrors()
        {
            _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
        }
    }
}
