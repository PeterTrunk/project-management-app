namespace ProjectManager.API.Services.GitBackfillService
{
    /// <summary>
    /// A szolgáltató API-gyökerének előállítása.
    ///
    /// Ez a saját telepítésű (self-hosted / self-managed) példányok miatt kell: a webhook
    /// irányban mindegy, hol áll a szolgáltató - ő hív minket -, a pótlásnál viszont NEKÜNK
    /// kell tudnunk, melyik kiszolgálót kérdezzük. Egy céges GitLab címe nem a gitlab.com.
    ///
    /// Az integráción tárolt cím a KISZOLGÁLÓ címe (pl. https://git.ceg.hu), a verziós
    /// útvonalat a szolgáltató szerinti fetcher teszi rá. Így a felhasználónak nem kell tudnia,
    /// hogy a GitLab API-ja /api/v4, a GitHub Enterprise-é pedig /api/v3 alatt van.
    /// </summary>
    public static class GitApiRoot
    {
        /// <summary>
        /// A használható API-gyökér: saját telepítésnél a megadott cím a verziós útvonallal,
        /// egyébként a szolgáltató felhős gyökere.
        ///
        /// A már megadott verziós útvonalat nem duplázza: a felhasználó beillesztheti a teljes
        /// API-címet is, és az alcsoportos (relative URL root alatt futó) telepítés is működik.
        /// </summary>
        public static string Resolve(string? instanceUrl, string cloudRoot, string versionPath)
        {
            if (string.IsNullOrWhiteSpace(instanceUrl)) return cloudRoot;

            var trimmed = instanceUrl.Trim().TrimEnd('/');

            return trimmed.EndsWith(versionPath, StringComparison.OrdinalIgnoreCase)
                ? trimmed
                : trimmed + versionPath;
        }
    }
}
