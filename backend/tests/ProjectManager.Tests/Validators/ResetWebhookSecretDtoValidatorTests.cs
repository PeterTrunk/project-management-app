using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Integration;
using ProjectManager.API.Validators.IntegrationValidators;

namespace ProjectManager.Tests.Validators
{
    public class ResetWebhookSecretDtoValidatorTests
    {
        private readonly ResetWebhookSecretDtoValidator _validator = new();

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void NewSecret_Empty_ShouldHaveError(string secret)
        {
            _validator.TestValidate(new ResetWebhookSecretDto { NewSecret = secret })
                .ShouldHaveValidationErrorFor(x => x.NewSecret);
        }

        //A secret HMAC kulcsként szolgál a webhook aláírás ellenőrzéséhez:
        //a minimális hossz itt biztonsági korlát, nem kényelmi.
        [Fact]
        public void NewSecret_Exactly15Chars_ShouldHaveError()
        {
            _validator.TestValidate(new ResetWebhookSecretDto { NewSecret = new string('a', 15) })
                .ShouldHaveValidationErrorFor(x => x.NewSecret);
        }

        [Fact]
        public void NewSecret_Exactly16Chars_ShouldNotHaveError()
        {
            _validator.TestValidate(new ResetWebhookSecretDto { NewSecret = new string('a', 16) })
                .ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void NewSecret_Long_ShouldNotHaveError()
        {
            _validator.TestValidate(new ResetWebhookSecretDto { NewSecret = new string('a', 256) })
                .ShouldNotHaveAnyValidationErrors();
        }
    }
}
