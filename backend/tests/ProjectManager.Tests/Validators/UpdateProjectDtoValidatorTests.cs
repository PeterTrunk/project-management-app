using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Project;
using ProjectManager.API.Validators.ProjectValidators;

namespace ProjectManager.Tests.Validators
{
    public class UpdateProjectDtoValidatorTests
    {
        private readonly UpdateProjectDtoValidator _validator = new();

        //Name

        [Fact]
        public void Name_Null_ShouldNotHaveError()
        {
            _validator.TestValidate(new UpdateProjectDto { Name = null })
                .ShouldNotHaveValidationErrorFor(x => x.Name);
        }

        //Figyelemre méltó: a CreateProjectDtoValidatorrel ellentétben itt nincs NotEmpty,
        //tehát az üres név átmegy a validátoron. A törlés elleni védelem a szolgáltatásé.
        [Fact]
        public void Name_Empty_ShouldNotHaveError()
        {
            _validator.TestValidate(new UpdateProjectDto { Name = "" })
                .ShouldNotHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Name_Exactly120Chars_ShouldNotHaveError()
        {
            _validator.TestValidate(new UpdateProjectDto { Name = new string('a', 120) })
                .ShouldNotHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Name_Exactly121Chars_ShouldHaveError()
        {
            _validator.TestValidate(new UpdateProjectDto { Name = new string('a', 121) })
                .ShouldHaveValidationErrorFor(x => x.Name);
        }

        //Description

        [Fact]
        public void Description_Null_ShouldNotHaveError()
        {
            _validator.TestValidate(new UpdateProjectDto { Description = null })
                .ShouldNotHaveValidationErrorFor(x => x.Description);
        }

        [Fact]
        public void Description_Exactly1000Chars_ShouldNotHaveError()
        {
            _validator.TestValidate(new UpdateProjectDto { Description = new string('a', 1000) })
                .ShouldNotHaveValidationErrorFor(x => x.Description);
        }

        [Fact]
        public void Description_Exactly1001Chars_ShouldHaveError()
        {
            _validator.TestValidate(new UpdateProjectDto { Description = new string('a', 1001) })
                .ShouldHaveValidationErrorFor(x => x.Description);
        }

        //A ProjKey szándékosan hiányzik a DTO-ból: a task kulcsok visszamenőleg érvénytelenné
        //válnának, ha a projekt kulcsa változhatna.
        [Fact]
        public void EmptyDto_ShouldNotHaveAnyErrors()
        {
            _validator.TestValidate(new UpdateProjectDto()).ShouldNotHaveAnyValidationErrors();
        }
    }
}
