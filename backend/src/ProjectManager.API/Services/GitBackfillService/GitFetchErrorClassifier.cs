using System.Net;

namespace ProjectManager.API.Services.GitBackfillService
{
    /// <summary>
    /// A szolgáltatói válasz besorolása. Mindkét fetcher ugyanezt használja, mert a besorolás
    /// nem a payload alakjáról szól, hanem a HTTP válaszról.
    /// </summary>
    public static class GitFetchErrorClassifier
    {
        /// <summary>
        /// Null, ha a válasz sikeres. Egyébként a besorolt hiba.
        /// </summary>
        public static GitFetchOutcome? Classify(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode) return null;

            //A 403 a GitHubnál KÉT dolgot jelent: jogosultsági hiba, VAGY elfogyott kvóta.
            //A kettőre ellentétes a helyes válasz (tokent cserélni, illetve várni és
            //újrapróbálni), ezért a maradék kvótát megkérdezzük, mielőtt döntünk.
            if (response.StatusCode == HttpStatusCode.Forbidden && IsRateLimited(response))
                return GitFetchOutcome.Fail(GitFetchErrorKind.Transient,
                    "A szolgáltató kérésszám-korlátja elfogyott, próbáld újra később.");

            return response.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                    GitFetchOutcome.Fail(GitFetchErrorKind.Authentication,
                        "A szolgáltató elutasította a hitelesítő adatot. Ellenőrizd a hozzáférési tokent."),

                //A GitHub a privát tárházak létét nem fedi fel: jogosultság nélkül is 404 a válasz.
                //Ezért a szöveg mindkét lehetőséget megnevezi.
                HttpStatusCode.NotFound =>
                    GitFetchOutcome.Fail(GitFetchErrorKind.NotFound,
                        "A szolgáltató nem találta a tárházat vagy a megadott commitokat. Lehet, hogy átnevezték, vagy a token nem látja."),

                HttpStatusCode.TooManyRequests =>
                    GitFetchOutcome.Fail(GitFetchErrorKind.Transient,
                        "A szolgáltató kérésszám-korlátja elfogyott, próbáld újra később."),

                //Az 5xx a szolgáltató oldali hiba: ezen egy későbbi kísérlet segíthet
                >= HttpStatusCode.InternalServerError =>
                    GitFetchOutcome.Fail(GitFetchErrorKind.Transient,
                        "A szolgáltató hibát jelzett. Próbáld újra később."),

                _ => GitFetchOutcome.Fail(GitFetchErrorKind.Permanent,
                        $"A szolgáltató elutasította a kérést ({(int)response.StatusCode}).")
            };
        }

        /// <summary>
        /// Kifogyott kvóta. A két szolgáltató más fejlécnevet használ, ezért mindkettőt
        /// megnézzük; ismeretlen fejléc mellett a válasz nemleges, tehát a hívó a
        /// státuszkód szerint dönt.
        /// </summary>
        private static bool IsRateLimited(HttpResponseMessage response)
        {
            foreach (var name in new[] { "x-ratelimit-remaining", "ratelimit-remaining" })
            {
                if (!response.Headers.TryGetValues(name, out var values)) continue;

                var raw = values.FirstOrDefault();
                if (int.TryParse(raw, out var remaining) && remaining <= 0) return true;
            }

            return false;
        }
    }
}
