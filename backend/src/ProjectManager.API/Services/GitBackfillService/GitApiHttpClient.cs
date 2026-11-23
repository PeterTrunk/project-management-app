namespace ProjectManager.API.Services.GitBackfillService
{
    /// <summary>
    /// A szolgáltatói API-hívásokhoz használt named HttpClient adatai.
    ///
    /// Azért named client, és nem új HttpClient példányok: a factory kezeli a kapcsolatok
    /// újrahasznosítását és a DNS élettartamot. Az időtúllépés itt szigorúbb a .NET
    /// alapértelmezésénél (100 s): a pótlás felhasználói kérésben fut, és egy beragadt
    /// kimenő hívás nem tarthatja a kérést percekig.
    /// </summary>
    public static class GitApiHttpClient
    {
        public const string Name = "git-provider";

        public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// A GitHub minden kérésnél elvárja a User-Agent fejlécet, és a GitLab naplójában is
        /// ebből látszik, ki kérdezett. Verziószám nincs benne: nem akarunk a kimenő
        /// kérésekben a telepítésről többet elárulni a szükségesnél.
        /// </summary>
        public const string UserAgent = "ProjectManager-PMA";
    }
}
