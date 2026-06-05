using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Sprints;
using ProjectManager.API.Validators.SprintValidators;
using System;
using System.Collections.Generic;
using System.Text;

namespace ProjectManager.Tests.Validators
{
    public class CreateSprintDtoValidatorTests
    {
        private readonly CreateSprintDtoValidator _validator = new();

        //Name

        [Fact]
        public void Name_Empty_ShouldHaveError()
        {
            var dto = new CreateSprintDto { Name = "" };
            var result = _validator.TestValidate(dto);
            result.ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Name_TooLong_ShouldHaveError()
        {
            var dto = new CreateSprintDto { Name = new string('a', 121) };
            var result = _validator.TestValidate(dto);
            result.ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Name_Valid_ShouldNotHaveError()
        {
            var dto = new CreateSprintDto { Name = "Sprint 1" };
            var result = _validator.TestValidate(dto);
            result.ShouldNotHaveValidationErrorFor(x => x.Name);
        }

        //Dátum logika

        [Fact]
        public void EndDate_BeforeStartDate_ShouldHaveError()
        {
            var dto = new CreateSprintDto
            {
                Name = "Sprint 1",
                StartDate = DateTime.UtcNow.AddDays(5),
                EndDate = DateTime.UtcNow.AddDays(1)
            };
            var result = _validator.TestValidate(dto);
            result.ShouldHaveValidationErrorFor(x => x.EndDate);
        }

        [Fact]
        public void EndDate_SameAsStartDate_ShouldHaveError()
        {
            var date = DateTime.UtcNow.AddDays(1);
            var dto = new CreateSprintDto
            {
                Name = "Sprint 1",
                StartDate = date,
                EndDate = date
            };
            var result = _validator.TestValidate(dto);
            result.ShouldHaveValidationErrorFor(x => x.EndDate);
        }

        [Fact]
        public void EndDate_AfterStartDate_ShouldNotHaveError()
        {
            var dto = new CreateSprintDto
            {
                Name = "Sprint 1",
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(14)
            };
            var result = _validator.TestValidate(dto);
            result.ShouldNotHaveValidationErrorFor(x => x.EndDate);
        }

        [Fact]
        public void BothDates_Null_ShouldNotHaveError()
        {
            var dto = new CreateSprintDto
            {
                Name = "Sprint 1",
                StartDate = null,
                EndDate = null
            };
            var result = _validator.TestValidate(dto);
            result.ShouldNotHaveValidationErrorFor(x => x.EndDate);
            result.ShouldNotHaveValidationErrorFor(x => x.StartDate);
        }

        [Fact]
        public void StartDate_NullEndDateSet_ShouldNotHaveError()
        {
            var dto = new CreateSprintDto
            {
                Name = "Sprint 1",
                StartDate = null,
                EndDate = DateTime.UtcNow.AddDays(14)
            };
            var result = _validator.TestValidate(dto);
            result.ShouldNotHaveValidationErrorFor(x => x.EndDate);
        }

        //Goal

        [Fact]
        public void Goal_TooLong_ShouldHaveError()
        {
            var dto = new CreateSprintDto
            {
                Name = "Sprint 1",
                Goal = new string('a', 501)
            };
            var result = _validator.TestValidate(dto);
            result.ShouldHaveValidationErrorFor(x => x.Goal);
        }

        [Fact]
        public void Goal_Null_ShouldNotHaveError()
        {
            var dto = new CreateSprintDto { Name = "Sprint 1", Goal = null };
            var result = _validator.TestValidate(dto);
            result.ShouldNotHaveValidationErrorFor(x => x.Goal);
        }

        [Fact]
        public void Goal_Exactly500Chars_ShouldNotHaveError()
        {
            var dto = new CreateSprintDto
            {
                Name = "Sprint 1",
                Goal = new string('a', 500)
            };
            var result = _validator.TestValidate(dto);
            result.ShouldNotHaveValidationErrorFor(x => x.Goal);
        }
    }
}
