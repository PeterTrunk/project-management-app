using System.Text.Json;

namespace ProjectManager.API.Services.GitWebhookService
{
    public interface IGitWebhookService
    {
        bool ValidateGitHubSignature(string payload, string signature);
        bool ValidateGitLabSignature(string token);

        Task ProcessPushEventAsync(Guid projectId, Guid integrationId, JsonElement payload);
        Task ProcessPullRequestEventAsync(Guid projectId, Guid integrationId, JsonElement payload);
    }
}
