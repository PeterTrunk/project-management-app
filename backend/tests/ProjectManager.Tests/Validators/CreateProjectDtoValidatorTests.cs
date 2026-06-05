using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Project;
using ProjectManager.API.Validators;
using ProjectManager.API.Validators.ProjectValidators;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace ProjectManager.Tests.Validators
{
    public class CreateProjectDtoValidatorTests
    {
        private readonly CreateProjectDtoValidator _validator = new();

        //Name

        [Fact]
        public void Name_Empty_ShouldHaveError()
        {
            var dto = new CreateProjectDto { Name = "", ProjKey = "PM" };
            var result = _validator.TestValidate(dto);
            result.ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Name_TooLong_ShouldHaveError()
        {
            var dto = new CreateProjectDto { Name = new string('a', 121), ProjKey = "PM" };
            var result = _validator.TestValidate(dto);
            result.ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Name_Valid_ShouldNotHaveError()
        {
            var dto = new CreateProjectDto { Name = "My Project", ProjKey = "PM" };
            var result = _validator.TestValidate(dto);
            result.ShouldNotHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Name_Exactly120Chars_ShouldNotHaveError()
        {
            var dto = new CreateProjectDto { Name = new string('a', 120), ProjKey = "PM" };
            var result = _validator.TestValidate(dto);
            result.ShouldNotHaveValidationErrorFor(x => x.Name);
        }

        //ProjKey

        [Fact]
        public void ProjKey_Empty_ShouldHaveError()
        {
            var dto = new CreateProjectDto { Name = "My Project", ProjKey = "" };
            var result = _validator.TestValidate(dto);
            result.ShouldHaveValidationErrorFor(x => x.ProjKey);
        }

        [Fact]
        public void ProjKey_TooShort_ShouldHaveError()
        {
            var dto = new CreateProjectDto { Name = "My Project", ProjKey = "P" };
            var result = _validator.TestValidate(dto);
            result.ShouldHaveValidationErrorFor(x => x.ProjKey);
        }

        [Fact]
        public void ProjKey_TooLong_ShouldHaveError()
        {
            var dto = new CreateProjectDto { Name = "My Project", ProjKey = new string('A', 11) };
            var result = _validator.TestValidate(dto);
            result.ShouldHaveValidationErrorFor(x => x.ProjKey);
        }

        [Fact]
        public void ProjKey_LowerCase_ShouldHaveError()
        {
            var dto = new CreateProjectDto { Name = "My Project", ProjKey = "pm" };
            var result = _validator.TestValidate(dto);
            result.ShouldHaveValidationErrorFor(x => x.ProjKey);
        }

        [Fact]
        public void ProjKey_SpecialCharacters_ShouldHaveError()
        {
            var dto = new CreateProjectDto { Name = "My Project", ProjKey = "PM-1" };
            var result = _validator.TestValidate(dto);
            result.ShouldHaveValidationErrorFor(x => x.ProjKey);
        }

        [Fact]
        public void ProjKey_WithSpaces_ShouldHaveError()
        {
            var dto = new CreateProjectDto { Name = "My Project", ProjKey = "PM 1" };
            var result = _validator.TestValidate(dto);
            result.ShouldHaveValidationErrorFor(x => x.ProjKey);
        }

        [Fact]
        public void ProjKey_ValidUpperCase_ShouldNotHaveError()
        {
            var dto = new CreateProjectDto { Name = "My Project", ProjKey = "PM" };
            var result = _validator.TestValidate(dto);
            result.ShouldNotHaveValidationErrorFor(x => x.ProjKey);
        }

        [Fact]
        public void ProjKey_ValidWithNumbers_ShouldNotHaveError()
        {
            var dto = new CreateProjectDto { Name = "My Project", ProjKey = "DEV123" };
            var result = _validator.TestValidate(dto);
            result.ShouldNotHaveValidationErrorFor(x => x.ProjKey);
        }

        [Fact]
        public void ProjKey_Exactly2Chars_ShouldNotHaveError()
        {
            var dto = new CreateProjectDto { Name = "My Project", ProjKey = "PM" };
            var result = _validator.TestValidate(dto);
            result.ShouldNotHaveValidationErrorFor(x => x.ProjKey);
        }

        [Fact]
        public void ProjKey_Exactly10Chars_ShouldNotHaveError()
        {
            var dto = new CreateProjectDto { Name = "My Project", ProjKey = new string('A', 10) };
            var result = _validator.TestValidate(dto);
            result.ShouldNotHaveValidationErrorFor(x => x.ProjKey);
        }

        //Description

        [Fact]
        public void Description_TooLong_ShouldHaveError()
        {
            var dto = new CreateProjectDto
            {
                Name = "My Project",
                ProjKey = "PM",
                Description = new string('a', 1001)
            };
            var result = _validator.TestValidate(dto);
            result.ShouldHaveValidationErrorFor(x => x.Description);
        }

        [Fact]
        public void Description_Null_ShouldNotHaveError()
        {
            var dto = new CreateProjectDto
            {
                Name = "My Project",
                ProjKey = "PM",
                Description = null
            };
            var result = _validator.TestValidate(dto);
            result.ShouldNotHaveValidationErrorFor(x => x.Description);
        }

        [Fact]
        public void Description_Exactly1000Chars_ShouldNotHaveError()
        {
            var dto = new CreateProjectDto
            {
                Name = "My Project",
                ProjKey = "PM",
                Description = new string('a', 1000)
            };
            var result = _validator.TestValidate(dto);
            result.ShouldNotHaveValidationErrorFor(x => x.Description);
        }

    }
}
