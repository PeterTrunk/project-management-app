namespace ProjectManager.API.Model
{
    public class Integration
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string RepoFullName { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string WebhookSecret { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Project Project { get; set; } = null!;
    }
}
