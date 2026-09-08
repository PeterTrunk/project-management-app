using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Sprints;
using ProjectManager.API.Validators.SprintValidators;

namespace ProjectManager.Tests.Validators
{
    public class AssignTaskToSprintDtoValidatorTests
    {
        private readonly AssignTaskToSprintDtoValidator _validator = new();

        //A sprint azonosítója az útvonalból jön, a törzs csak a konkurenciavezérlés
        //bélyegét hordozza - ezért a nulla itt hiányzó mezőt jelent.
        [Fact]
        public void RowVersion_Zero_ShouldHaveError()
        {
            _validator.TestValidate(new AssignTaskToSprintDto { RowVersion = 0 })
                .ShouldHaveValidationErrorFor(x => x.RowVersion);
        }

        [Theory]
        [InlineData(1u)]
        [InlineData(uint.MaxValue)]
        public void RowVersion_Positive_ShouldNotHaveError(uint rowVersion)
        {
            _validator.TestValidate(new AssignTaskToSprintDto { RowVersion = rowVersion })
                .ShouldNotHaveAnyValidationErrors();
        }
    }
}
