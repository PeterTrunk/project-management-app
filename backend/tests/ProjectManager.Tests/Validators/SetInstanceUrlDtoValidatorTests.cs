using FluentValidation.TestHelper;
using ProjectManager.API.DTOs.Integration;
using ProjectManager.API.Validators.IntegrationValidators;

namespace ProjectManager.Tests.Validators
{
    /// <summary>
    /// A saját telepítésű szolgáltató címe.
    ///
    /// A szabály szigorúbb egy egyszerű „legyen URL"-nél, mert ezt a címet a szerver FOGJA
    /// HÍVNI, a tárolt hozzáférési tokennel a fejlécben.
    /// </summary>
    public class SetInstanceUrlDtoValidatorTests
    {
        private readonly SetInstanceUrlDtoValidator _validator = new();

        private void Accepts(string? url) =>
            _validator.TestValidate(new SetInstanceUrlDto { InstanceUrl = url })
                .ShouldNotHaveAnyValidationErrors();

        private void Rejects(string? url) =>
            _validator.TestValidate(new SetInstanceUrlDto { InstanceUrl = url })
                .ShouldHaveValidationErrorFor(x => x.InstanceUrl);

        //Az üres érték a felhős változatra való visszaállás
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Empty_ShouldNotHaveError(string? url) => Accepts(url);

        [Theory]
        [InlineData("https://git.ceg.hu")]
        [InlineData("https://git.ceg.hu/")]
        [InlineData("https://git.ceg.hu:8443")]
        [InlineData("https://ceg.hu/gitlab")]
        [InlineData("https://git.ceg.hu/api/v4")]
        public void ValidServerAddress_ShouldNotHaveError(string url) => Accepts(url);

        //A token a kérés fejlécében utazik: titkosítatlan csatornán nem adjuk ki
        [Fact]
        public void PlainHttp_ShouldHaveError() => Rejects("http://git.ceg.hu");

        /// <summary>
        /// A felhasználónevet tartalmazó alak hitelesítő adatot csempészne egy olyan mezőbe,
        /// amely - a tokennel ellentétben - titkosítás nélkül tárolódik.
        /// </summary>
        [Fact]
        public void CredentialsInTheUrl_ShouldHaveError() => Rejects("https://user:pass@git.ceg.hu");

        //Ezek a végpont útvonalához fűződnének, és nem az a cím keletkezne, amit a felhasználó lát
        [Theory]
        [InlineData("https://git.ceg.hu?token=abc")]
        [InlineData("https://git.ceg.hu#fragment")]
        public void QueryOrFragment_ShouldHaveError(string url) => Rejects(url);

        [Theory]
        [InlineData("git.ceg.hu")]
        [InlineData("nem egy cím")]
        [InlineData("ftp://git.ceg.hu")]
        [InlineData("javascript:alert(1)")]
        public void NotAnHttpsAddress_ShouldHaveError(string url) => Rejects(url);

        [Fact]
        public void TooLong_ShouldHaveError() =>
            Rejects("https://git.ceg.hu/" + new string('a', InstanceUrlRule.MaxLength));
    }
}
