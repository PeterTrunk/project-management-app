namespace ProjectManager.API.Model
{
    public class Integration
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string RepoFullName { get; set; } = string.Empty;
        public string WebhookSecret { get; set; } = string.Empty;
        public string WebhookToken { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public bool IsVerified { get; set; } = false;

        /// <summary>
        /// Saját telepítésű szolgáltató kiszolgálójának címe (pl. https://git.ceg.hu), vagy
        /// null, ha a szolgáltató felhős változatáról van szó.
        ///
        /// A webhook irányban erre nincs szükség: a szolgáltató hív minket, bárhol is áll.
        /// A pótlásnál viszont NEKÜNK kell tudnunk, melyik kiszolgálót kérdezzük - és egy
        /// céges GitLab címe nem a gitlab.com. A verziós útvonalat (/api/v4, illetve /api/v3)
        /// a szolgáltató szerinti lekérő teszi rá, tehát ide a kiszolgáló címe kerül.
        /// </summary>
        public string? InstanceUrl { get; set; }

        /// <summary>
        /// A szolgáltató API-jának hívásához használt hozzáférési token, <b>titkosítva</b>
        /// tárolva (AES-GCM, ugyanúgy, mint a WebhookSecret).
        ///
        /// Ez az oszlop korábban LÉTEZETT, és tudatosan töröltük (RemoveUnusedIntegrationAccessToken),
        /// mert semmi nem használta: egy sosem használt hitelesítő adat törlése jobb, mint a
        /// titkosítása. A döntés feltétele változott meg, nem a döntés volt hibás: a szolgáltatók
        /// levágják a webhook payload commit tömbjét, és a kimaradt commitok CSAK az API-n
        /// kérhetők le. Mostantól van mire használni.
        ///
        /// Nullozható, és az is marad: publikus tárházból a commitok hitelesítés nélkül is
        /// olvashatók (a GitHub ilyenkor IP-nként 60 kérést enged óránként, tokennel 5000-et),
        /// privát tárháznál viszont token nélkül nincs pótlás.
        /// </summary>
        public string? AccessToken { get; set; }

        /// <summary>
        /// Igaz, ha hiányos push észlelésekor a rendszer magától megpróbálja pótolni a kimaradt
        /// commitokat - EGYSZER. Alapértelmezésben hamis.
        ///
        /// Azért integrációnkénti beállítás, mert a pótlás a szolgáltató kvótáját fogyasztja,
        /// token nélkül pedig IP-nkénti, tehát KÖZÖS korlátot: egy beszédes integráció a
        /// többiét is elhasználná. A bekapcsolás így tudatos vállalás.
        /// </summary>
        public bool AutoBackfillEnabled { get; set; } = false;

        /// <summary>
        /// Mikor nyilatkozott a felvevő arról, hogy jogosult a repository csatlakoztatására,
        /// és tudomásul vette a commit szerzők adatainak tárolását.
        ///
        /// Nullozható: a mező bevezetése előtt felvett integrációkra visszamenőleg nem
        /// állítható elő nyilatkozat. Ez nem hozzájárulás a GDPR értelmében, 
        /// a repository kezelője nem nyilatkozhat a commit szerzők nevében, 
        /// hanem szavatosság és a jogos érdeken alapuló adatkezelés átláthatóvá tétele.
        /// </summary>
        public DateTime? AuthorityConfirmedAt { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Project Project { get; set; } = null!;
        public ICollection<CommitLink> CommitLinks { get; set; } = new List<CommitLink>();
        public ICollection<PrLink> PrLinks { get; set; } = new List<PrLink>();
    }
}
