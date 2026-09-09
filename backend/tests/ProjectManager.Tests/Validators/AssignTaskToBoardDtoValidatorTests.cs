using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.ProjectTask;
using ProjectManager.API.Validators.ProjectTaskValidators;

namespace ProjectManager.Tests.Validators
{
    public class AssignTaskToBoardDtoValidatorTests
    {
        private readonly AssignTaskToBoardDtoValidator _validator = new();

        //A null BoardId a boardról való levételt jelenti (vissza a backlogba),
        //ezért nem lehet rajta NotEmpty szabály.
        [Fact]
        public void BoardId_Null_ShouldNotHaveError()
        {
            _validator.TestValidate(new AssignTaskToBoardDto { BoardId = null, RowVersion = 42 })
                .ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void BoardId_RealGuid_ShouldNotHaveError()
        {
            _validator.TestValidate(new AssignTaskToBoardDto { BoardId = Guid.NewGuid(), RowVersion = 42 })
                .ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void RowVersion_Zero_ShouldHaveError()
        {
            _validator.TestValidate(new AssignTaskToBoardDto { BoardId = Guid.NewGuid(), RowVersion = 0 })
                .ShouldHaveValidationErrorFor(x => x.RowVersion);
        }

        [Theory]
        [InlineData(1u)]
        [InlineData(uint.MaxValue)]
        public void RowVersion_Positive_ShouldNotHaveError(uint rowVersion)
        {
            _validator.TestValidate(new AssignTaskToBoardDto { RowVersion = rowVersion })
                .ShouldNotHaveValidationErrorFor(x => x.RowVersion);
        }
    }
}
