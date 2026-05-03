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

        public ProjectTask? ProjectTask { get; set; }
        public Integration Integration { get; set; } = null!;
    }
}
