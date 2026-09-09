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
        /// amivel a tervezett felhasználó-összekapcsolás elvégezhető. Amíg az a funkció nincs
        /// kész, az értéket nem küldjük ki a felületre - lásd CommitLinkResponseDto.
        /// </summary>
        public string AuthorEmail { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public DateTime CommittedAt { get; set; }

        public ProjectTask? ProjectTask { get; set; }
        public Integration Integration { get; set; } = null!;
    }
}
