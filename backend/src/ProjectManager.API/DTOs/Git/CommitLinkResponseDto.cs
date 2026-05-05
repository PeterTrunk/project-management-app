namespace ProjectManager.API.DTOs.Git
{
    public class CommitLinkResponseDto
    {
        public Guid Id { get; set; }
        public string CommitSha { get; set; } = string.Empty;
        public string? CommitUrl { get; set; }
        public string Message { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public string AuthorEmail { get; set; } = string.Empty;
        public DateTime CommittedAt { get; set; }
    }
}
