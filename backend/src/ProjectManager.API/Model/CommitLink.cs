namespace ProjectManager.API.Model
{
    public class CommitLink
    {
        public Guid Id { get; set; }
        public Guid? TaskId { get; set; }
        public string Provider {  get; set; } = string.Empty;
        public string RepoFullName { get; set; } = string.Empty;
        public string CommitSha {  get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string AuthorEmail {  get; set; } = string.Empty;
        public DateTime CommitedAt { get; set; }

        public ProjectTask? ProjektTask { get; set; }
    }
}
