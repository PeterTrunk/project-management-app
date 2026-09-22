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
