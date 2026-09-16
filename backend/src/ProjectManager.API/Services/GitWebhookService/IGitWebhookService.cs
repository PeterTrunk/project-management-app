using ProjectManager.API.Services.GitWebhookService.Payloads;

namespace ProjectManager.API.Services.GitWebhookService
{
    public interface IGitWebhookService
    {
        bool ValidateGitHubSignature(string payload, string signature, string secret);
        bool ValidateGitLabSignature(string token, string secret);
        Task ProcessPushEventAsync(Guid projectId, Guid integrationId, string provider, GitPushEvent pushEvent);
        Task ProcessPullRequestEventAsync(Guid projectId, Guid integrationId, string provider, GitPullRequestEvent prEvent);
    }
}
