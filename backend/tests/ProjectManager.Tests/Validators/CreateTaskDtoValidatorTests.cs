using FluentValidation.TestHelper;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.DTOs.ProjectTask;
using ProjectManager.API.DTOs.ProjectTaskValidators;

namespace ProjectManager.Tests.Validators
{
    public class CreateTaskDtoValidatorTests
    {
        private readonly CreateTaskDtoValidator _validator = new();

        private static CreateTaskDto Valid() => new() { Title = "Hibajavítás" };

        //Title

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Title_Empty_ShouldHaveError(string title)
        {
            var dto = Valid();
            dto.Title = title;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Title);
        }

        [Fact]
        public void Title_Exactly200Chars_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.Title = new string('a', 200);
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Title);
        }

        [Fact]
        public void Title_Exactly201Chars_ShouldHaveError()
        {
            var dto = Valid();
            dto.Title = new string('a', 201);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Title);
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
        public void Description_Exactly250Chars_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.Description = new string('a', 250);
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Description);
        }

        [Fact]
        public void Description_Exactly251Chars_ShouldHaveError()
        {
            var dto = Valid();
            dto.Description = new string('a', 251);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Description);
        }

        //Priority

        [Fact]
        public void Priority_Null_ShouldNotHaveError()
        {
            //A prioritás nem kötelező: az alapértéket a szolgáltatás adja
            var dto = Valid();
            dto.Priority = null;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Priority);
        }

        [Theory]
        [InlineData(TaskPrioritys.None)]
        [InlineData(TaskPrioritys.Low)]
        [InlineData(TaskPrioritys.Medium)]
        [InlineData(TaskPrioritys.High)]
        [InlineData(TaskPrioritys.Critical)]
        public void Priority_KnownValue_ShouldNotHaveError(string priority)
        {
            var dto = Valid();
            dto.Priority = priority;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Priority);
        }

        //A lista összehasonlítása kis- és nagybetűérzékeny: a frontend fix értékeket küld,
        //de egy közvetlen API hívás eltérő írásmóddal érvénytelen adatot vinne az adatbázisba.
        [Theory]
        [InlineData("")]
        [InlineData("Low")]
        [InlineData("HIGH")]
        [InlineData("urgent")]
        [InlineData("blocker")]
        public void Priority_UnknownValue_ShouldHaveError(string priority)
        {
            var dto = Valid();
            dto.Priority = priority;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Priority);
        }

        //DueDate

        [Fact]
        public void DueDate_Null_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.DueDate = null;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.DueDate);
        }

        //A szabály a validátor példányosításakor rögzíti az akkori UtcNow-t. A validátorok
        //kérésenkénti (scoped) élettartama miatt ez a gyakorlatban mindig friss érték.
        [Fact]
        public void DueDate_InThePast_ShouldHaveError()
        {
            var dto = Valid();
            dto.DueDate = DateTime.UtcNow.AddDays(-1);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.DueDate);
        }

        [Fact]
        public void DueDate_InTheFuture_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.DueDate = DateTime.UtcNow.AddDays(1);
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.DueDate);
        }

        //EstimateInMinutes

        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        [InlineData(-30)]
        [InlineData(480)]
        public void EstimateInMinutes_AnyValue_ShouldNotHaveError(int? estimate)
        {
            //Ma nincs szabály a becslésre - ha valaha lesz, ez a teszt bukik el elsőként
            var dto = Valid();
            dto.EstimateInMinutes = estimate;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.EstimateInMinutes);
        }

        [Fact]
        public void ValidDto_ShouldNotHaveAnyErrors()
        {
            _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
        }
    }
}
