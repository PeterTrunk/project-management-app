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

        [Fact]
        public void State_Planning_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.State = SprintStates.Planning;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.State);
        }

        //Letrehozni CSAK Planning allapotban lehet. Az "Active" tiltasa a lenyegi allitas:
        //azon az uton lehetett megkerulni az "mar van aktiv sprint" ellenorzest, es ket
        //aktiv sprintet letrehozni ugyanabban a projektben.
        //A "Completed" ugyanezert tiltott: az allapotgep nem indulhat a vegerol.
        [Theory]
        [InlineData(SprintStates.Active)]
        [InlineData(SprintStates.Completed)]
        public void State_NotPlanning_ShouldHaveError(string state)
        {
            var dto = Valid();
            dto.State = state;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.State);
        }

        //Az összehasonlítás kis- és nagybetűérzékeny
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

        //Ez a teszt korábban azt állította, hogy a létrehozó validátor MINDEN deklarált
        //állapotot elfogad. Ez a szigorítás után már nem igaz, és nem is lenne helyes:
        //létrehozni csak Planning állapotban lehet.
        //
        //A védő szerepe viszont megmarad, csak máshogy: ha a SprintStates bővül, ez a teszt
        //elbukik, és akkor el KELL dönteni, hogy az új állapot létrehozható-e. Enélkül egy
        //új állapot csendben bekerülne a rendszerbe anélkül, hogy bárki végiggondolta volna,
        //honnan érhető el - és pontosan ilyen csendes úton lehetett eddig "Active" sprintet
        //létrehozni, megkerülve az aktiválás ellenőrzését.
        [Fact]
        public void TheDeclaredStateSet_IsTheKnownOne()
        {
            Assert.Equal(
                new[] { SprintStates.Planning, SprintStates.Active, SprintStates.Completed },
                SprintStates.ValidStates);
        }

        //A nem létrehozható állapotokat a State_NotPlanning_ShouldHaveError fedi; ez itt
        //azt köti le, hogy a kettő halmaza együtt kiadja a teljes deklarált halmazt.
        [Fact]
        public void EveryDeclaredState_IsEitherCreatableOrExplicitlyRejected()
        {
            foreach (var state in SprintStates.ValidStates)
            {
                var dto = Valid();
                dto.State = state;
                var result = _validator.TestValidate(dto);

                if (state == SprintStates.Planning)
                    result.ShouldNotHaveValidationErrorFor(x => x.State);
                else
                    result.ShouldHaveValidationErrorFor(x => x.State);
            }
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
