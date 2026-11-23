namespace ProjectManager.API.DTOs.Git
{
    /// <summary>
    /// Egy hiányosan érkezett push a felületen.
    ///
    /// A szöveges hibaok benne van, mert enélkül a felhasználó egy megfoghatatlan „sikertelen"
    /// állapotot látna, és nem tudná eldönteni, érdemes-e újrapróbálni vagy tokent cserélni.
    /// A szolgáltató nyers válasza viszont SOSEM kerül ide: a hibaok a mi megfogalmazásunk.
    /// </summary>
    public class CommitBackfillResponseDto
    {
        public Guid Id { get; set; }
        public Guid IntegrationId { get; set; }

        public string Provider { get; set; } = string.Empty;
        public string RepoFullName { get; set; } = string.Empty;

        /// <summary>Az ág, amelyre a push érkezett. Null, ha a szolgáltató nem küldte.</summary>
        public string? Ref { get; set; }

        /// <summary>Amennyit a szolgáltató ígért; null, ha nem közöl darabszámot (GitHub).</summary>
        public int? ReportedCommitCount { get; set; }

        public int ReceivedCommitCount { get; set; }

        public string State { get; set; } = string.Empty;

        public int AttemptCount { get; set; }
        public string? LastError { get; set; }

        /// <summary>
        /// Indítható-e a pótlás. Hamis, ha a push azonosítói hiányoznak (ilyenkor nincs mihez
        /// képest lekérdezni), ha az integráció le van tiltva, vagy ha már pótolva van.
        /// A felület ebből tiltja a gombot, a kikényszerítés viszont a szolgáltatásban van.
        /// </summary>
        public bool CanRun { get; set; }

        /// <summary>
        /// Igaz, ha a legutóbbi kísérletet a rendszer indította, nem ember. Így a felületen
        /// kiírható, hogy a kudarc nem a felhasználó mulasztása - különben indok nélküli
        /// hibát látna.
        /// </summary>
        public bool LastAttemptWasAutomatic { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? ClaimedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
