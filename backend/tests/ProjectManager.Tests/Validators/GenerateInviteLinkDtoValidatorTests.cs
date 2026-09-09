using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Team;
using ProjectManager.API.Validators.TeamValidators;

namespace ProjectManager.Tests.Validators
{
    /// <summary>
    /// A meghívó link egy projekt tagságát osztja: a lejárat és a felhasználásszám a
    /// korlátozás két eszköze, ezért a felső határ itt biztonsági szabály.
    /// </summary>
    public class GenerateInviteLinkDtoValidatorTests
    {
        private readonly GenerateInviteLinkDtoValidator _validator = new();

        //ExpiresInDays

        [Fact]
        public void ExpiresInDays_Null_ShouldNotHaveError()
        {
            //A null az alapértelmezett lejáratot jelenti, amit a szolgáltatás ad
            _validator.TestValidate(new GenerateInviteLinkDto { ExpiresInDays = null })
                .ShouldNotHaveValidationErrorFor(x => x.ExpiresInDays);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public void ExpiresInDays_NotPositive_ShouldHaveError(int days)
        {
            _validator.TestValidate(new GenerateInviteLinkDto { ExpiresInDays = days })
                .ShouldHaveValidationErrorFor(x => x.ExpiresInDays);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(15)]
        [InlineData(30)]
        public void ExpiresInDays_WithinRange_ShouldNotHaveError(int days)
        {
            _validator.TestValidate(new GenerateInviteLinkDto { ExpiresInDays = days })
                .ShouldNotHaveValidationErrorFor(x => x.ExpiresInDays);
        }

        [Theory]
        [InlineData(31)]
        [InlineData(365)]
        [InlineData(int.MaxValue)]
        public void ExpiresInDays_AboveThirty_ShouldHaveError(int days)
        {
            _validator.TestValidate(new GenerateInviteLinkDto { ExpiresInDays = days })
                .ShouldHaveValidationErrorFor(x => x.ExpiresInDays);
        }

        //MaxUses

        [Fact]
        public void MaxUses_Null_ShouldNotHaveError()
        {
            //A null a korlátlan felhasználást jelenti
            _validator.TestValidate(new GenerateInviteLinkDto { MaxUses = null })
                .ShouldNotHaveValidationErrorFor(x => x.MaxUses);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void MaxUses_NotPositive_ShouldHaveError(int maxUses)
        {
            _validator.TestValidate(new GenerateInviteLinkDto { MaxUses = maxUses })
                .ShouldHaveValidationErrorFor(x => x.MaxUses);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(100)]
        [InlineData(int.MaxValue)]
        public void MaxUses_Positive_ShouldNotHaveError(int maxUses)
        {
            //Felső korlát szándékosan nincs: a lejárat önmagában is behatárolja a linket
            _validator.TestValidate(new GenerateInviteLinkDto { MaxUses = maxUses })
                .ShouldNotHaveValidationErrorFor(x => x.MaxUses);
        }

        [Fact]
        public void EmptyDto_ShouldNotHaveAnyErrors()
        {
            _validator.TestValidate(new GenerateInviteLinkDto()).ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void ValidDto_ShouldNotHaveAnyErrors()
        {
            _validator.TestValidate(new GenerateInviteLinkDto { ExpiresInDays = 7, MaxUses = 5 })
                .ShouldNotHaveAnyValidationErrors();
        }
    }
}
