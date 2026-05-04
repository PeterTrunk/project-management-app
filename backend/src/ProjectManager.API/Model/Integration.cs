namespace ProjectManager.API.Model
{
    public class Integration
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string RepoFullName { get; set; } = string.Empty;
        public string? AccessToken { get; set; }
        public string WebhookSecret { get; set; } = string.Empty;
        public string WebhookToken { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public bool IsVerified { get; set; } = false;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Project Project { get; set; } = null!;
        public ICollection<CommitLink> CommitLinks { get; set; } = new List<CommitLink>();
        public ICollection<PrLink> PrLinks { get; set; } = new List<PrLink>();
    }
}
