using ProjectManager.API.Common.Constants;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace ProjectManager.Tests.Common
{
    /// <summary>
    /// A jogi dokumentumok verziója két helyen szerepel: a backend
    /// <see cref="LegalDocuments.CurrentVersion"/> konstansában és a frontend
    /// <c>src/lib/legal.ts</c> LEGAL_VERSION értékében. A kettőnek egyeznie KELL.
    ///
    /// Ha elcsúsznak, a regisztráció MINDEN kérésre elbukik: a kliens a saját verzióját küldi,
    /// a szerver pedig csak a hatályosat fogadja el. Ez egy egysoros figyelmetlenségből teljes
    /// regisztrációs leállás - ezért itt, fordításkor bukjon el, ne élesben.
    ///
    /// Szándékosan NEM környezeti változóból jön egyik sem: a verzió azt mondja meg, melyik
    /// SZÖVEGET fogadta el a felhasználó, a szöveg pedig a repóban van. Egy env-be kiszervezett
    /// verzió elcsúszhatna környezetek között, és a git történetből sem derülne ki, mikor mi
    /// változott. (Az adatkezelő nevét és címét ezzel szemben helyes env-ből venni: az
    /// telepítés-specifikus, nem tartalom-specifikus.)
    /// </summary>
    public class LegalVersionTests
    {
        //A CallerFilePath fordításkor oldódik fel, tehát a CI checkout-jában is helyes útvonalat ad.
        //Innen négy szint felfelé a repó gyökere:
        //  <gyökér>/backend/tests/ProjectManager.Tests/Common/LegalVersionTests.cs
        private static string FrontendLegalPath([CallerFilePath] string callerPath = "")
        {
            var repoRoot = Path.GetFullPath(
                Path.Combine(Path.GetDirectoryName(callerPath)!, "..", "..", "..", ".."));

            return Path.Combine(repoRoot, "frontend", "src", "lib", "legal.ts");
        }

        private static string ReadFrontendVersion()
        {
            var path = FrontendLegalPath();

            Assert.True(File.Exists(path),
                $"Nem található a frontend legal.ts: {path}. Ha a fájl elmozdult, ezt a tesztet is frissíteni kell.");

            var source = File.ReadAllText(path);
            var match = Regex.Match(source, @"LEGAL_VERSION\s*=\s*'([^']+)'");

            Assert.True(match.Success,
                "A frontend legal.ts nem tartalmaz felismerhető LEGAL_VERSION értéket.");

            return match.Groups[1].Value;
        }

        [Fact]
        public void BackendAndFrontendVersions_Match()
        {
            var frontendVersion = ReadFrontendVersion();

            Assert.Equal(LegalDocuments.CurrentVersion, frontendVersion);
        }

        //A verzió dátum alapú, és ugyanazt a napot kell jelentenie, mint a hatálybalépés.
        //Egy félbehagyott verzióváltás (a string átírva, a dátum nem) így nem marad észrevétlen.
        [Fact]
        public void CurrentVersion_MatchesItsEffectiveDate()
        {
            var parsed = DateTime.ParseExact(
                LegalDocuments.CurrentVersion, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

            Assert.Equal(parsed.Date, LegalDocuments.CurrentVersionEffectiveFrom.Date);
        }

        [Fact]
        public void CurrentVersionEffectiveFrom_IsUtc()
        {
            //Az EffectiveFrom timestamptz oszlopba kerül; a nem UTC érték némán eltolódna
            Assert.Equal(DateTimeKind.Utc, LegalDocuments.CurrentVersionEffectiveFrom.Kind);
        }
    }
}
