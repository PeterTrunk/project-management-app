using ProjectManager.API.Services.GitWebhookService;

namespace ProjectManager.Tests.Services.GitWebhook
{
    /// <summary>
    /// A task kulcsok kiolvasása commit üzenetből, PR címből és leírásból.
    ///
    /// Ezen szabályok döntik el, mi kapcsolódik mihez, tehát a határesetek számítanak ha:
    /// - egy túl bőkezű minta idegen szövegre is illeszkedne 
    /// - egy túl szigorú pedig észrevétlenül hagyná a felhasználó szándékát
    /// Mindkét irány néma: nincs hibaüzenet, csak egy összekapcsolás, ami vagy létrejön vagy sem.
    /// </summary>
    public class TaskKeyMatcherTests
    {
        private const string ProjKey = "PMA";

        //Elfogadott alakok
        [Theory]
        [InlineData("PMA-1 hibajavítás")]
        [InlineData("hibajavítás PMA-1")]
        [InlineData("javítás a PMA-1 taskhoz")]
        [InlineData("#PMA-1 hibajavítás")]
        [InlineData("[PMA-1] hibajavítás")]
        [InlineData("(PMA-1) hibajavítás")]
        [InlineData("fix: valami (PMA-1)")]
        [InlineData("Lezárja a PMA-1.")]
        [InlineData("Érinti a PMA-1, és mást is")]
        [InlineData("Kész a PMA-1!")]
        [InlineData("PMA-1")]
        public void ExtractTaskKeys_AcceptedForms(string text)
        {
            Assert.Equal(["PMA-1"], TaskKeyMatcher.ExtractTaskKeys(ProjKey, text));
        }

        [Fact]
        public void ExtractTaskKeys_IsCaseInsensitiveAndNormalisesToUpperCase()
        {
            //A TaskKey az adatbázisban nagybetűs, tehát a kiolvasott értéknek is annak kell lennie
            Assert.Equal(["PMA-1"], TaskKeyMatcher.ExtractTaskKeys(ProjKey, "pma-1 javítás"));
            Assert.Equal(["PMA-1"], TaskKeyMatcher.ExtractTaskKeys(ProjKey, "Pma-1 javítás"));
        }

        [Fact]
        public void ExtractTaskKeys_MultipleKeys_AreAllReturned()
        {
            var keys = TaskKeyMatcher.ExtractTaskKeys(ProjKey, "PMA-1 és PMA-2 együtt");

            Assert.Equal(["PMA-1", "PMA-2"], keys);
        }

        [Fact]
        public void ExtractTaskKeys_SameKeyTwice_IsReturnedOnce()
        {
            var keys = TaskKeyMatcher.ExtractTaskKeys(ProjKey, "PMA-1 javítás, lásd még PMA-1");

            Assert.Equal(["PMA-1"], keys);
        }

        [Fact]
        public void ExtractTaskKeys_MultiDigitNumber_IsReadWhole()
        {
            Assert.Equal(["PMA-123"], TaskKeyMatcher.ExtractTaskKeys(ProjKey, "PMA-123 javítás"));
        }

        //Elutasított alakok
        [Theory]
        //Betű tapad elé: ami másik projekt kulcsa is lehetne
        [InlineData("XPMA-1 javítás")]
        //Karakter tapad mögé: nem ez a kulcs
        [InlineData("PMA-1x javítás")]
        [InlineData("PMA-1-2 javítás")]
        //Szám lemaradt: nincs kulcsként értelmezve
        [InlineData("PMA- javítás")]
        [InlineData("PMA javítás")]
        //Más projekt kulcsa
        [InlineData("OTHER-1 javítás")]
        public void ExtractTaskKeys_RejectedForms(string text)
        {
            Assert.Empty(TaskKeyMatcher.ExtractTaskKeys(ProjKey, text));
        }
        
        //Egy URL-ben szereplő kulcsot nem vesszük találatnak, mert a "/" nem áll a megengedett előtagok között.
        //Így egy taskra mutató LINK önmagában nem kapcsol össze semmit - a kulcsot ki kell írni a szövegbe.
        //(Bár jelenleg nincs ilyen megosztási lehetőség)
        [Fact]
        public void ExtractTaskKeys_KeyInsideUrl_IsNotAMatch()
        {
            Assert.Empty(TaskKeyMatcher.ExtractTaskKeys(ProjKey, "lásd https://pma.local/tasks/PMA-1"));
        }

        //Üres bemenetek
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ExtractTaskKeys_EmptyText_ReturnsNothing(string? text)
        {
            Assert.Empty(TaskKeyMatcher.ExtractTaskKeys(ProjKey, text));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ExtractTaskKeys_EmptyProjectKey_ReturnsNothing(string? projKey)
        {
            Assert.Empty(TaskKeyMatcher.ExtractTaskKeys(projKey!, "PMA-1 javítás"));
        }

        /// <summary>
        /// A projekt kulcsa a felhasználótól jön. 
        /// A validátor ma csak [A-Z0-9]-t enged, de ha valaha lazul,
        /// egy escape nélküli minta regex-injektálást nyitna egy olyan illesztésben,
        /// ami egy idegen webhook payloadon fut.
        /// </summary>
        [Fact]
        public void ExtractTaskKeys_EscapesTheProjectKey()
        {
            //Escape nélkül a "." bármilyen karakterre illeszkedne, tehát az "AXB-1" is találat lenne
            Assert.Empty(TaskKeyMatcher.ExtractTaskKeys("A.B", "AXB-1 javítás"));

            //A szó szerinti kulcs viszont igen
            Assert.Equal(["A.B-1"], TaskKeyMatcher.ExtractTaskKeys("A.B", "A.B-1 javítás"));
        }

        //Cím és leírás összefűzése - ez az etap lényegi képessége
        [Fact]
        public void CombineTitleAndBody_NoBody_ReturnsTheTitleOnly()
        {
            Assert.Equal("Cím", TaskKeyMatcher.CombineTitleAndBody("Cím", null));
            Assert.Equal("Cím", TaskKeyMatcher.CombineTitleAndBody("Cím", ""));
            Assert.Equal("Cím", TaskKeyMatcher.CombineTitleAndBody("Cím", "   "));
        }

        [Fact]
        public void CombineTitleAndBody_SeparatesWithANewLine()
        {
            //Az elválasztó nem lehet üres: enélkül a cím vége és a leírás eleje összeragadna,
            //és egy sor végi kulcs elveszne
            Assert.Equal("Cím\nLeírás", TaskKeyMatcher.CombineTitleAndBody("Cím", "Leírás"));
        }

        [Fact]
        public void KeyOnlyInTheBody_IsFound()
        {
            var text = TaskKeyMatcher.CombineTitleAndBody(
                "Kulcs nélküli cím",
                "Ez a PR a PMA-1 taskot zárja le.");

            Assert.Equal(["PMA-1"], TaskKeyMatcher.ExtractTaskKeys(ProjKey, text));
        }

        [Fact]
        public void KeyAtTheEndOfTheTitle_SurvivesTheJoin()
        {
            //A cím utolsó karaktere a kulcs: az újsor elválasztó teszi találattá
            var text = TaskKeyMatcher.CombineTitleAndBody("Javítás PMA-1", "Bővebb leírás.");

            Assert.Equal(["PMA-1"], TaskKeyMatcher.ExtractTaskKeys(ProjKey, text));
        }

        [Fact]
        public void KeysInTitleAndBody_AreBothFound()
        {
            var text = TaskKeyMatcher.CombineTitleAndBody(
                "PMA-1 hibajavítás",
                "Egyúttal a PMA-2 is elkészült.");

            Assert.Equal(["PMA-1", "PMA-2"], TaskKeyMatcher.ExtractTaskKeys(ProjKey, text));
        }

        [Fact]
        public void KeyOnItsOwnLineInTheBody_IsFound()
        {
            //A sablonos PR leírások gyakran külön sorba teszik a hivatkozást
            var text = TaskKeyMatcher.CombineTitleAndBody(
                "Refaktor",
                "## Leírás\nValami szöveg.\n\nPMA-1\n\n## Tesztelés\nKézzel.");

            Assert.Equal(["PMA-1"], TaskKeyMatcher.ExtractTaskKeys(ProjKey, text));
        }

        [Fact]
        public void WindowsLineEndingsInTheBody_DoNotBreakMatching()
        {
            var text = TaskKeyMatcher.CombineTitleAndBody("Refaktor", "Első sor.\r\nPMA-1\r\nHarmadik sor.");

            Assert.Equal(["PMA-1"], TaskKeyMatcher.ExtractTaskKeys(ProjKey, text));
        }
    }
}
