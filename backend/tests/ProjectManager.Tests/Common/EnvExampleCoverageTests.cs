using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace ProjectManager.Tests.Common
{
    /// <summary>
    /// A `.env.example` a telepítés egyetlen hivatkozási pontja: 
    /// a README is erre mutat a teljes változólistáért, és katasztrófa után ebből kell újraépíteni a környezetet.
    ///
    /// Ez a teszt azért létezik, mert a fájl **észrevétlenül** szokott elavulni. 
    /// Egy új `GetEnvironmentVariable` hívás vagy egy új `${...}` a compose fájlban működik a fejlesztő gépén
    /// - ahol az érték már be van állítva -, és csak egy tiszta környezetben derül ki, hogy soha nem került dokumentálásra.
    /// Két változó (`ENCRYPTION_KEY`, `JWT_REFRESH_TOKEN_LIFETIME`) hiányában az API **el sem indul**; 
    /// pontosan ez az állapot állt fenn, amíg ez a teszt nem létezett.
    ///
    /// Ugyanaz a minta, mint a <see cref="LegalVersionTests"/>-nél: a <c>[CallerFilePath]</c>
    /// fordításkor oldódik fel, tehát a CI checkoutjában is helyes útvonalat ad.
    /// </summary>
    public class EnvExampleCoverageTests
    {
        // <gyökér>/backend/tests/ProjectManager.Tests/Common/EnvExampleCoverageTests.cs
        private static string RepoRoot([CallerFilePath] string callerPath = "") =>
            Path.GetFullPath(
                Path.Combine(Path.GetDirectoryName(callerPath)!, "..", "..", "..", ".."));

        private static string ReadRepoFile(params string[] relativeParts)
        {
            var path = Path.Combine(RepoRoot(), Path.Combine(relativeParts));

            Assert.True(File.Exists(path),
                $"Nem található: {path}. Ha a fájl elmozdult, ezt a tesztet is frissíteni kell.");

            return File.ReadAllText(path);
        }

        /// <summary>A `.env.example`-ben deklarált kulcsok (a kommentek nem számítanak).</summary>
        private static HashSet<string> DocumentedKeys() =>
            Regex.Matches(ReadRepoFile(".env.example"), @"^([A-Z0-9_]+)=", RegexOptions.Multiline)
                .Select(m => m.Groups[1].Value)
                .ToHashSet();

        /// <summary>
        /// Amit NEM a telepítő állít be, hanem a futtatókörnyezet ad.
        ///
        /// Az <c>ASPNETCORE_ENVIRONMENT</c>-et fejlesztéskor a <c>launchSettings.json</c>, élesben
        /// pedig a <c>docker-compose.prod.yml</c> írja be közvetlen értékként - nem <c>${...}</c>
        /// behelyettesítéssel. A `.env`-be felvenni félrevezető lenne: azt sugallná, hogy ott
        /// kell állítani, pedig az ottani érték nem jutna el az alkalmazásig.
        /// </summary>
        private static readonly HashSet<string> HostProvidedKeys = ["ASPNETCORE_ENVIRONMENT"];

        /// <summary>
        /// Amit a rendszer tényleg igényel, három forrásból:
        /// a compose behelyettesítései, a backend olvasásai és a frontend típusdeklarációja.
        /// </summary>
        private static HashSet<string> RequiredKeys()
        {
            var keys = new HashSet<string>();

            //A compose ${...} behelyettesítései - ezek nélkül a konténerek hibásan indulnak
            keys.UnionWith(
                Regex.Matches(ReadRepoFile("docker-compose.prod.yml"), @"\$\{([A-Z0-9_]+)\}")
                    .Select(m => m.Groups[1].Value));

            //A backend olvasásai. A Program.cs fail-fast ága ezek egy részét kötelezővé teszi.
            keys.UnionWith(
                Regex.Matches(
                    ReadRepoFile("backend", "src", "ProjectManager.API", "Program.cs"),
                    @"GetEnvironmentVariable\(""([A-Z0-9_]+)""\)")
                    .Select(m => m.Groups[1].Value));

            //A frontend VITE_* változói. A vite-env.d.ts a kanonikus lista:
            //a vite/client index-szignatúrája miatt egy elgépelt név NEM fordítási hiba,
            //tehát ez a deklaráció az egyetlen hely, ahol a teljes készlet látszik.
            keys.UnionWith(
                Regex.Matches(
                    ReadRepoFile("frontend", "src", "vite-env.d.ts"),
                    @"readonly (VITE_[A-Z0-9_]+)")
                    .Select(m => m.Groups[1].Value));

            keys.ExceptWith(HostProvidedKeys);

            return keys;
        }

        [Fact]
        public void EveryRequiredVariable_IsDocumented()
        {
            var missing = RequiredKeys().Except(DocumentedKeys()).OrderBy(k => k).ToList();

            Assert.True(missing.Count == 0,
                "A .env.example-ből hiányzik, pedig a rendszer igényli: "
                + string.Join(", ", missing)
                + ". Vedd fel a fájlba egy megjegyzéssel arról, mi történik nélküle.");
        }

        /// <summary>
        /// A fordított irány: egy dokumentált, de már senki által nem olvasott változó félrevezető
        /// - a telepítő beállít valamit, aminek nincs hatása. A `GIT_WEBHOOK_SECRET` pontosan ilyen volt: 
        /// a webhook titok integrációnként van, titkosítva az adatbázisban, a globális változót viszont senki nem törölte.
        /// </summary>
        [Fact]
        public void NoDocumentedVariable_IsUnused()
        {
            var stale = DocumentedKeys().Except(RequiredKeys()).OrderBy(k => k).ToList();

            Assert.True(stale.Count == 0,
                "A .env.example dokumentálja, de semmi nem hivatkozza: "
                + string.Join(", ", stale)
                + ". Vagy használatban van és a fenti három forrás egyikéből kimaradt, vagy törölni kell.");
        }

        /// <summary>
        /// Az értékek üresen maradnak: ez a fájl a repóban van, tehát egy beírt titok
        /// azonnal a git történetbe kerülne.
        /// </summary>
        [Fact]
        public void NoVariable_HasAValue()
        {
            //A karakterosztály szándékosan zárja ki a \r-t: a fájl CRLF sorvégű, és a `.`
            //illeszkedik a kocsivisszára - egy naiv `(.+)$` minden sort "értékesnek" látna.
            var withValue = Regex.Matches(
                    ReadRepoFile(".env.example"), @"^([A-Z0-9_]+)=([^\r\n]+)$", RegexOptions.Multiline)
                .Select(m => m.Groups[1].Value)
                .ToList();

            Assert.True(withValue.Count == 0,
                "A .env.example-ben érték szerepel, pedig a fájl a repóban van: "
                + string.Join(", ", withValue)
                + ". A példaértékek a megjegyzésbe tartoznak, nem az értékhez.");
        }
    }
}
