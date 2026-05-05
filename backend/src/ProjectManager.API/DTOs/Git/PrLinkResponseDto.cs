namespace ProjectManager.API.DTOs.Git
{
    public class PrLinkResponseDto
    {
        public Guid Id { get; set; }
        public int PrNumber { get; set; }
        public string? PrUrl { get; set; }
        public string Title { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? MergedAt { get; set; }
    }
}
