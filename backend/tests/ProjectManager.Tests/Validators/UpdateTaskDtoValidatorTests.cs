using FluentValidation.TestHelper;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.DTOs.ProjectTask;
using ProjectManager.API.DTOs.ProjectTaskValidators;

namespace ProjectManager.Tests.Validators
{
    public class UpdateTaskDtoValidatorTests
    {
        private readonly UpdateTaskDtoValidator _validator = new();

        private static UpdateTaskDto Valid() => new() { Title = "Hibajavítás", RowVersion = 42 };

        //Title

        [Fact]
        public void Title_Null_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.Title = null;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Title);
        }

        //Eltérés a létrehozástól: itt nincs NotEmpty, tehát az üres cím átmegy a validátoron
        [Fact]
        public void Title_Empty_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.Title = "";
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Title);
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

        [Theory]
        [InlineData("")]
        [InlineData("Low")]
        [InlineData("urgent")]
        public void Priority_UnknownValue_ShouldHaveError(string priority)
        {
            var dto = Valid();
            dto.Priority = priority;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Priority);
        }

        //DueDate

        //Eltérés a létrehozástól: frissítéskor a múltbeli határidő megengedett, mert egy
        //régi taskot is lehet szerkeszteni anélkül, hogy a lejárt határidőt át kellene írni.
        [Fact]
        public void DueDate_InThePast_ShouldNotHaveError()
        {
            var dto = Valid();
            dto.DueDate = DateTime.UtcNow.AddDays(-30);
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.DueDate);
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
        public void OnlyRowVersion_ShouldNotHaveAnyErrors()
        {
            _validator.TestValidate(new UpdateTaskDto { RowVersion = 7 })
                .ShouldNotHaveAnyValidationErrors();
        }
    }
}
