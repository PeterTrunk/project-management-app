using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Integration;
using ProjectManager.API.Validators.IntegrationValidators;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace ProjectManager.Tests.Validators
{
    public class CreateIntegrationDtoValidatorTests
    {
        private readonly CreateIntegrationDtoValidator _validator = new();

        //Provider

        [Fact]
        public void Provider_Empty_ShouldHaveError()
        {
            var dto = new CreateIntegrationDto { Provider = "", RepoFullName = "owner/repo", WebhookSecret = "mysecret12345678" };
            var result = _validator.TestValidate(dto);
            result.ShouldHaveValidationErrorFor(x => x.Provider);
        }

        [Fact]
        public void Provider_Invalid_ShouldHaveError()
        {
            var dto = new CreateIntegrationDto { Provider = "Bitbucket", RepoFullName = "owner/repo", WebhookSecret = "mysecret12345678" };
            var result = _validator.TestValidate(dto);
            result.ShouldHaveValidationErrorFor(x => x.Provider);
        }

        [Fact]
        public void Provider_GitHub_ShouldNotHaveError()
        {
            var dto = new CreateIntegrationDto { Provider = "GitHub", RepoFullName = "owner/repo", WebhookSecret = "mysecret12345678" };
            var result = _validator.TestValidate(dto);
            result.ShouldNotHaveValidationErrorFor(x => x.Provider);
        }

        [Fact]
        public void Provider_GitLab_ShouldNotHaveError()
        {
            var dto = new CreateIntegrationDto { Provider = "GitLab", RepoFullName = "owner/repo", WebhookSecret = "mysecret12345678" };
            var result = _validator.TestValidate(dto);
            result.ShouldNotHaveValidationErrorFor(x => x.Provider);
        }

        //RepoFullName

        [Fact]
        public void RepoFullName_Empty_ShouldHaveError()
        {
            var dto = new CreateIntegrationDto { Provider = "GitHub", RepoFullName = "", WebhookSecret = "mysecret12345678" };
            var result = _validator.TestValidate(dto);
            result.ShouldHaveValidationErrorFor(x => x.RepoFullName);
        }

        [Fact]
        public void RepoFullName_WithoutSlash_ShouldHaveError()
        {
            var dto = new CreateIntegrationDto { Provider = "GitHub", RepoFullName = "ownerrepo", WebhookSecret = "mysecret12345678" };
            var result = _validator.TestValidate(dto);
            result.ShouldHaveValidationErrorFor(x => x.RepoFullName);
        }

        [Fact]
        public void RepoFullName_OnlySlash_ShouldHaveError()
        {
            var dto = new CreateIntegrationDto { Provider = "GitHub", RepoFullName = "/", WebhookSecret = "mysecret12345678" };
            var result = _validator.TestValidate(dto);
            result.ShouldHaveValidationErrorFor(x => x.RepoFullName);
        }

        [Fact]
        public void RepoFullName_Valid_ShouldNotHaveError()
        {
            var dto = new CreateIntegrationDto { Provider = "GitHub", RepoFullName = "owner/repo", WebhookSecret = "mysecret12345678" };
            var result = _validator.TestValidate(dto);
            result.ShouldNotHaveValidationErrorFor(x => x.RepoFullName);
        }

        [Fact]
        public void RepoFullName_WithHyphensAndDots_ShouldNotHaveError()
        {
            var dto = new CreateIntegrationDto { Provider = "GitHub", RepoFullName = "my-owner/my-repo.js", WebhookSecret = "mysecret12345678" };
            var result = _validator.TestValidate(dto);
            result.ShouldNotHaveValidationErrorFor(x => x.RepoFullName);
        }

        //WebhookSecret

        [Fact]
        public void WebhookSecret_Empty_ShouldHaveError()
        {
            var dto = new CreateIntegrationDto { Provider = "GitHub", RepoFullName = "owner/repo", WebhookSecret = "" };
            var result = _validator.TestValidate(dto);
            result.ShouldHaveValidationErrorFor(x => x.WebhookSecret);
        }

        [Fact]
        public void WebhookSecret_TooShort_ShouldHaveError()
        {
            var dto = new CreateIntegrationDto { Provider = "GitHub", RepoFullName = "owner/repo", WebhookSecret = "short" };
            var result = _validator.TestValidate(dto);
            result.ShouldHaveValidationErrorFor(x => x.WebhookSecret);
        }

        [Fact]
        public void WebhookSecret_Exactly15Chars_ShouldHaveError()
        {
            var dto = new CreateIntegrationDto { Provider = "GitHub", RepoFullName = "owner/repo", WebhookSecret = new string('a', 15) };
            var result = _validator.TestValidate(dto);
            result.ShouldHaveValidationErrorFor(x => x.WebhookSecret);
        }

        [Fact]
        public void WebhookSecret_Exactly16Chars_ShouldNotHaveError()
        {
            var dto = new CreateIntegrationDto { Provider = "GitHub", RepoFullName = "owner/repo", WebhookSecret = new string('a', 16) };
            var result = _validator.TestValidate(dto);
            result.ShouldNotHaveValidationErrorFor(x => x.WebhookSecret);
        }

        [Fact]
        public void WebhookSecret_Valid_ShouldNotHaveError()
        {
            var dto = new CreateIntegrationDto { Provider = "GitHub", RepoFullName = "owner/repo", WebhookSecret = "mysecret12345678" };
            var result = _validator.TestValidate(dto);
            result.ShouldNotHaveValidationErrorFor(x => x.WebhookSecret);
        }
    }
}
