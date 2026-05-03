namespace ProjectManager.API.DTOs.Integration
{
    public class CreateIntegrationDto
    {
        /// <summary>
        /// pl. GitHub vagy GitLab
        /// </summary>
        public string Provider { get; set; } = string.Empty;
        /// <summary>
        /// pl. owner/repo
        /// </summary>
        public string RepoFullName { get; set; } = string.Empty;
        /// <summary>
        /// Opcionális GitHub/GitLab access token
        /// </summary>
        public string? AccessToken { get; set; }
    }
}
