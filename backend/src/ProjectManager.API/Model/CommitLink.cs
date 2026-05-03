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
        public string AuthorEmail { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public DateTime CommittedAt { get; set; }

        public ProjectTask? ProjectTask { get; set; }
        public Integration Integration { get; set; } = null!;
    }
}
