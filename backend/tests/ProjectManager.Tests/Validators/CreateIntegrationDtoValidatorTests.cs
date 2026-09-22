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

        /// <summary>
        /// A GitLab enged alcsoportokat, és ott a projekt útvonala kettőnél több szegmensből
        /// áll. A mező CÍMKE - a webhookot az URL-beli token azonosítja, a titok hitelesíti -,
        /// tehát a kétszegmenses korlát nem védett semmit, csak arra kényszerítette a
        /// felhasználót, hogy pontatlan nevet írjon be.
        /// </summary>
        [Theory]
        [InlineData("csoport/alcsoport/projekt")]
        [InlineData("cegnev/csapat/alcsapat/repo")]
        public void RepoFullName_GitLabSubgroupPath_ShouldNotHaveError(string repoFullName)
        {
            var dto = new CreateIntegrationDto { Provider = "GitLab", RepoFullName = repoFullName, WebhookSecret = "mysecret12345678" };
            var result = _validator.TestValidate(dto);
            result.ShouldNotHaveValidationErrorFor(x => x.RepoFullName);
        }

        //A lazítás nem nyithatja meg a nyilvánvalóan hibás alakokat
        [Theory]
        [InlineData("owner/")]
        [InlineData("/repo")]
        [InlineData("owner//repo")]
        [InlineData("owner/repo/")]
        public void RepoFullName_MalformedPath_ShouldHaveError(string repoFullName)
        {
            var dto = new CreateIntegrationDto { Provider = "GitLab", RepoFullName = repoFullName, WebhookSecret = "mysecret12345678" };
            var result = _validator.TestValidate(dto);
            result.ShouldHaveValidationErrorFor(x => x.RepoFullName);
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

        //AuthorityConfirmed

        private static CreateIntegrationDto Valid() => new()
        {
            Provider = "GitHub",
            RepoFullName = "owner/repo",
            WebhookSecret = "mysecret12345678",
            AuthorityConfirmed = true
        };

        //A felület letiltja a gombot pipa nélkül, de az API nyilvános: egy közvetlen kérés
        //megkerülné a jelölőnégyzetet, ezért a szerveroldali kikényszerítés a lényegi védelem.
        [Fact]
        public void AuthorityConfirmed_False_ShouldHaveError()
        {
            var dto = Valid();
            dto.AuthorityConfirmed = false;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.AuthorityConfirmed);
        }

        [Fact]
        public void AuthorityConfirmed_Missing_ShouldHaveError()
        {
            //A bool alapértéke false: a mezőt kihagyó kérés is elutasításra kerül
            var dto = new CreateIntegrationDto
            {
                Provider = "GitHub",
                RepoFullName = "owner/repo",
                WebhookSecret = "mysecret12345678"
            };
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.AuthorityConfirmed);
        }

        [Fact]
        public void AuthorityConfirmed_True_ShouldNotHaveError()
        {
            _validator.TestValidate(Valid()).ShouldNotHaveValidationErrorFor(x => x.AuthorityConfirmed);
        }

        [Fact]
        public void ValidDto_ShouldNotHaveAnyErrors()
        {
            _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
        }
    }
}
