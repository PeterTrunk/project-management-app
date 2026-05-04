namespace ProjectManager.API.DTOs.Integration
{
    public class IntegrationResponseDto
    {
        public Guid Id { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string RepoFullName { get; set; } = string.Empty;
        public string WebhookToken { get; set; } = string.Empty;
        public string WebhookUrl { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public bool IsVerified { get; set; }
        public bool HasAccessToken { get; set; }  // token értékét nem adjuk vissza!
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
