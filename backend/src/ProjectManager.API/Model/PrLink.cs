namespace ProjectManager.API.Model
{
    public class PrLink
    {
        public Guid Id { get; set; }
        public Guid? TaskId { get; set; }
        public Guid IntegrationId { get; set; }
        public int PrNumber { get; set; }
        public string? PrUrl { get; set; }
        public string Title { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? MergedAt { get; set; }

        /// <summary>
        /// Igaz, ha ezt a hozzárendelést EMBER állította be, nem az illesztő.
        ///
        /// A jelentése egy mondatban: ehhez valaki hozzányúlt, ne bíráljuk felül. 
        /// A webhook ezért kihagyja az automatikus illesztést arra a commitra / pull requestre, 
        /// amelyhez tartozik kézzel beállított sor - különben egy újabb esemény (forcepush, szerkesztés, merge) 
        /// újra megtalálná az eredeti, HIBÁS kulcsot, és a javítás visszacsinálná.
        ///
        /// Az állapot- és üzenetfrissítés viszont továbbra is lefut.
        /// </summary>
        public bool IsManuallyLinked { get; set; }

        public ProjectTask? ProjectTask { get; set; }
        public Integration Integration { get; set; } = null!;
    }
}
