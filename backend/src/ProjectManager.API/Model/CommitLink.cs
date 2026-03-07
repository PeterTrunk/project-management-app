namespace ProjectManager.API.Model
{
    public class CommitLink
    {
        public Guid Id { get; set; }
        public Guid? TaskId { get; set; }
        public string Provider {  get; set; } = string.Empty;
        public string RepoFullName { get; set; } = string.Empty;
        public string CommitSha {  get; set; } = string.Empty;
        public string? CommitUrl { get; set; }
        public string Message { get; set; } = string.Empty;
        public string AuthorEmail {  get; set; } = string.Empty;
        public DateTime CommittedAt { get; set; }

        public ProjectTask? ProjectTask { get; set; }
    }
}
