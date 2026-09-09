using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Attachment;
using ProjectManager.API.Validators.AttachmentValidators;

namespace ProjectManager.Tests.Validators
{
    public class ConfirmUploadDtoValidatorTests
    {
        private readonly ConfirmUploadDtoValidator _validator = new();

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void StorageKey_Empty_ShouldHaveError(string key)
        {
            _validator.TestValidate(new ConfirmUploadDto { StorageKey = key })
                .ShouldHaveValidationErrorFor(x => x.StorageKey);
        }

        //A kulcs alakját a szolgáltatás ellenőrzi: azt kell igazolnia, hogy a kulcs a hívó
        //projektjéhez tartozik. Egy formai szabály itt hamis biztonságérzetet adna.
        [Theory]
        [InlineData("projects/1a2b/tasks/3c4d/terv.pdf")]
        [InlineData("barmi")]
        public void StorageKey_NonEmpty_ShouldNotHaveError(string key)
        {
            _validator.TestValidate(new ConfirmUploadDto { StorageKey = key })
                .ShouldNotHaveAnyValidationErrors();
        }
    }
}
