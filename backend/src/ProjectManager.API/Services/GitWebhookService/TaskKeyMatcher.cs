using System.Text.RegularExpressions;

namespace ProjectManager.API.Services.GitWebhookService
{
    /// <summary>
    /// Task kulcsok kiolvasása szabad szövegből: commit üzenetből, PR címből és leírásból.
    ///
    /// A projekt kulcsát paraméterként kapja, nem adatbázisból olvassa.
    /// (Tesztelési okból is megkönnyíti hogy a tesztekben nem kell bonyolultabb Dockeres megoldás)
    /// </summary>
    public static class TaskKeyMatcher
    {
        //ReDoS elleni védelem.
        //A minta nem visszalépő, de a ProjKey a felhasználótól jön:
        //a korlát akkor is áll, ha a validátor valaha lazul.
        private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// A PR címe és leírása egyetlen illesztendő szöveggé. 
        /// Újsorral, mert az elválasztó karakternek szóköznek kell lennie: 
        /// enélkül a cím vége és a leírás eleje összeragadna, és egy sor végi kulcs elveszne.
        /// </summary>
        public static string CombineTitleAndBody(string title, string? body) =>
            string.IsNullOrWhiteSpace(body) ? title : $"{title}\n{body}";

        /// <summary>
        /// A szövegben szereplő task kulcsok, nagybetűsítve és duplikátumok nélkül.
        ///
        /// Elfogadott alakok: <c>PMA-1</c>, <c>#PMA-1</c>, <c>[PMA-1]</c>, <c>(PMA-1)</c> -
        /// a kulcs előtt szóköz, sorkezdet, szögletes/kerek zárójel vagy kettőskereszt állhat,
        /// utána szóköz, sorvég, záró zárójel vagy mondatzáró írásjel.
        /// A körülhatárolás lényeges: enélkül a <c>PMA-12</c> a <c>PMA-1</c> keresésére is illeszkedne.
        /// </summary>
        public static IReadOnlyList<string> ExtractTaskKeys(string projKey, string? text)
        {
            if (string.IsNullOrWhiteSpace(projKey) || string.IsNullOrWhiteSpace(text))
                return [];

            //A ProjKey escape-elve kerül a mintába: a validátor jelenleg csak [A-Z0-9]-t enged,
            //de egyetlen lazítása regex-injektálást nyitna egy webhook payloadon futó illesztésben.
            var pattern = $@"(?:^|[\s\[(\#])({Regex.Escape(projKey)}-\d+)(?:$|[\s\])\.,!])";

            try
            {
                return Regex.Matches(text, pattern,
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout)
                    .Select(m => m.Groups[1].Value.ToUpperInvariant())
                    .Distinct()
                    .ToList();
            }
            catch (RegexMatchTimeoutException)
            {
                //Fail-closed: találat nélkül a commit hozzárendeletlenként kerül be, ami később hozzárendelhető taskhoz.
                //Nem adhatunk kivételt (500-as kódot) mert a szolgáltató idővel kikapcsolja a webhookot.
                return [];
            }
        }
    }
}
