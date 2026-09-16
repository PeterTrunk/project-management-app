namespace ProjectManager.API.Services.GitWebhookService.Payloads
{
    /// <summary>
    /// A webhook esemény típusa, szolgáltatótól függetlenül.
    /// A providerek eltérő fejlécben és eltérő néven küldik ugyanazt (push vs. Push Hook),
    /// ezért a fejléc értelmezése a parser dolga, és a controller már csak ezekkel az esetekkel számol.
    /// </summary>
    public enum WebhookEventKind
    {
        /// <summary>Nem ismerjük, vagy szándékosan nem kezeljük. A végpont ilyenkor 200-at ad.</summary>
        Unknown = 0,

        /// <summary>A webhook felvételekor küldött próbaesemény. Csak a GitHub küld ilyet.</summary>
        Ping,

        Push,
        PullRequest
    }
}
