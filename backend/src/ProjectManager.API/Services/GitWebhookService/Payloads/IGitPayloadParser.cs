using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace ProjectManager.API.Services.GitWebhookService.Payloads
{
    /// <summary>
    /// Egy git szolgáltató payloadjait fordítja le a általánosan használható alakra.
    ///
    /// Ez a réteg azért van, mert a szolgáltatók ugyanarra a fogalomra más nevet és más szerkezetet használnak,
    /// ezért enélkül a feldolgozó szolgáltatás GitHub-specifikus JSON-t  olvasott a GitLab payloadjából is.
    /// Ez kivétellel szállt el, így nem mondhattuk támogatottnak a GitLab-ot.
    ///
    /// Az implementációk szándékosan függőség nélküliek: 
    /// Se adatbázis, se naplózó, se óra.
    /// Így tiszta függvények, Docker nélkül tesztelhetők, és esetleges mintapayloadok önmagukban elegendő bemenetek.
    /// </summary>
    public interface IGitPayloadParser
    {
        /// <summary>
        /// Melyik Integration.Provider értékhez tartozik. Lásd GitProviders.
        /// </summary>
        string Provider { get; }

        /// <summary>
        /// Az a HTTP fejléc, amelyben ez a szolgáltató az esemény nevét küldi.
        /// </summary>
        string EventHeaderName { get; }

        /// <summary>
        /// A fejléc értékének lefordítása. Amit nem kezelünk az Unknown értékű.
        /// </summary>
        WebhookEventKind ResolveEventKind(string headerValue);

        /// <summary>
        /// Hamis, ha a payload nem értelmezhető push eseményként. 
        /// A hívó ilyenkor csendben átugorja az eseményt, ez hibát NEM jelez a szolgáltató felé, 
        /// mert egy 500-as válasz miatt a szolgáltató kikapcsolhatná a webhookot.
        /// </summary>
        bool TryParsePush(JsonElement payload, [NotNullWhen(true)] out GitPushEvent? result);

        /// <summary>
        /// Hamis, ha a payload nem értelmezhető, VAGY olyan akcióról szól, amire nem reagálunk
        /// (például GitLab jóváhagyás). A két esetet a hívó nem különbözteti meg: mindkettőnél
        /// ugyanaz a helyes válasz, hogy nem történik semmi.
        /// </summary>
        bool TryParsePullRequest(JsonElement payload, [NotNullWhen(true)] out GitPullRequestEvent? result);
    }
}
