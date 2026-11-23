using ProjectManager.API.Services.GitBackfillService;

namespace ProjectManager.Tests.Services.GitBackfill
{
    /// <summary>
    /// Az API-gyökér előállítása. Ez a saját telepítésű példányok miatt van: a webhook irányban
    /// mindegy, hol áll a szolgáltató, a pótlásnál viszont nekünk kell tudnunk, kit kérdezzünk.
    ///
    /// A felhasználótól a KISZOLGÁLÓ címét kérjük, nem az API címét - a verziós útvonalat a kód
    /// teszi rá. Ezek a tesztek azt a néhány alakot zárják le, amit egy felhasználó tényleg
    /// beír: záró perjel, beillesztett teljes API-cím, alcsoport alatti telepítés.
    /// </summary>
    public class GitApiRootTests
    {
        private const string Cloud = "https://gitlab.com/api/v4";
        private const string VersionPath = "/api/v4";

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void NoInstanceUrl_UsesTheCloudRoot(string? instanceUrl)
        {
            Assert.Equal(Cloud, GitApiRoot.Resolve(instanceUrl, Cloud, VersionPath));
        }

        [Fact]
        public void SelfHosted_GetsTheVersionPath()
        {
            Assert.Equal(
                "https://git.ceg.hu/api/v4",
                GitApiRoot.Resolve("https://git.ceg.hu", Cloud, VersionPath));
        }

        //Záró perjel nélkül kettős perjeles cím keletkezne
        [Fact]
        public void TrailingSlash_DoesNotDoubleUp()
        {
            Assert.Equal(
                "https://git.ceg.hu/api/v4",
                GitApiRoot.Resolve("https://git.ceg.hu/", Cloud, VersionPath));
        }

        /// <summary>
        /// A felhasználó beillesztheti a teljes API-címet is. Ilyenkor a verziós útvonal
        /// hozzáfűzése duplázást adna, és minden kérés 404-re futna - ez pont az a hibafajta,
        /// amit a felületen nehéz megérteni.
        /// </summary>
        [Fact]
        public void AlreadyAnApiRoot_IsLeftAlone()
        {
            Assert.Equal(
                "https://git.ceg.hu/api/v4",
                GitApiRoot.Resolve("https://git.ceg.hu/api/v4", Cloud, VersionPath));

            Assert.Equal(
                "https://git.ceg.hu/api/v4",
                GitApiRoot.Resolve("https://git.ceg.hu/api/v4/", Cloud, VersionPath));
        }

        //A GitLab futhat egy útvonal alatt is (relative URL root)
        [Fact]
        public void SubPathInstallation_KeepsThePath()
        {
            Assert.Equal(
                "https://ceg.hu/gitlab/api/v4",
                GitApiRoot.Resolve("https://ceg.hu/gitlab", Cloud, VersionPath));
        }

        /// <summary>
        /// A GitHubnál a felhő NEM a példány címéből képződik: az API külön hoszton van
        /// (api.github.com), az Enterprise Server viszont a példány alatt, /api/v3-on.
        /// Ezért nem elég egyetlen szabály, és ezért kap a két fetcher külön gyökeret.
        /// </summary>
        [Fact]
        public void GitHubCloudAndEnterprise_DifferInShape()
        {
            Assert.Equal(
                "https://api.github.com",
                GitApiRoot.Resolve(null, "https://api.github.com", "/api/v3"));

            Assert.Equal(
                "https://ghe.ceg.hu/api/v3",
                GitApiRoot.Resolve("https://ghe.ceg.hu", "https://api.github.com", "/api/v3"));
        }
    }
}
