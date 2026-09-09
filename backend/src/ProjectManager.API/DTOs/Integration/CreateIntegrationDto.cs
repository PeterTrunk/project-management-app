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

        /// <summary>
        /// Webhook secret
        /// </summary>
        public string WebhookSecret { get; set; } = string.Empty;

        /// <summary>
        /// A felvevő nyilatkozata arról, hogy jogosult a repository csatlakoztatására, és
        /// tudomásul veszi, hogy a rendszer eltárolja a beérkező commitok szerzőjének nevét
        /// és e-mail címét. A felület letiltja a gombot pipa nélkül, de az API közvetlenül
        /// is hívható - a kikényszerítés a validátoré.
        /// </summary>
        public bool AuthorityConfirmed { get; set; }
    }
}
