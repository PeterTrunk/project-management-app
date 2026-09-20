namespace ProjectManager.API.Model
{
    public class CommitLink
    {
        public Guid Id { get; set; }
        public Guid? TaskId { get; set; }
        public Guid IntegrationId { get; set; }
        public string CommitSha { get; set; } = string.Empty;
        public string? CommitUrl { get; set; }
        public string Message { get; set; } = string.Empty;
        /// <summary>
        /// A cél a commit-task összekapcsolása, ezen belül ez a mező a szerző azonosítója,
        /// amivel a tervezett felhasználó-összekapcsolás elvégezhető. 
        /// Amíg az a funkció nincs kész, az értéket nem küldjük ki a felületre (CommitLinkResponseDto).
        /// </summary>
        public string AuthorEmail { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public DateTime CommittedAt { get; set; }

        /// <summary>
        /// Igaz, ha ezt a hozzárendelést EMBER állította be, nem az illesztő.
        ///
        /// A jelentése egy mondatban: ehhez valaki hozzányúlt, ne bíráljuk felül. 
        /// A webhook ezért kihagyja az automatikus illesztést arra a commitra / pull requestre, 
        /// amelyhez tartozik kézzel beállított sor - különben egy újabb esemény (forcepush, szerkesztés,merge)
        /// újra megtalálná az eredeti, HIBÁS kulcsot, és a javítás visszacsinálná.
        ///
        /// Az állapot- és üzenetfrissítés viszont továbbra is lefut.
        /// </summary>
        public bool IsManuallyLinked { get; set; }

        public ProjectTask? ProjectTask { get; set; }
        public Integration Integration { get; set; } = null!;
    }
}
