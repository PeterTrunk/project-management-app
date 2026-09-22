namespace ProjectManager.API.DTOs.Git
{
    public class LinkedPrResponseDto
    {
        public Guid Id { get; set; }
        public int PrNumber { get; set; }
        public string? PrUrl { get; set; }
        public string Title { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? MergedAt { get; set; }
        public bool IsManuallyLinked { get; set; }

        public Guid? TaskId { get; set; }
        public string? TaskKey { get; set; }
        public string? TaskTitle { get; set; }
    }
}
