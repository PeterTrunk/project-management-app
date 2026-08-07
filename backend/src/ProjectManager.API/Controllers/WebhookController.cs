using Microsoft.AspNetCore.Mvc;
using ProjectManager.API.Services.EncryptionService;
using ProjectManager.API.Services.GitWebhookService;
using ProjectManager.API.Services.IntegrationService;
using System.Text.Json;

namespace ProjectManager.API.Controllers
{
    [ApiController]
    [Route("api/git/webhook")]
    public class WebhookController : ControllerBase
    {
        private readonly IGitWebhookService _gitWebhookService;
        private readonly IIntegrationService _integrationService;
        private readonly IEncryptionService _encryptionService;

        public WebhookController(
            IGitWebhookService gitWebhookService, 
            IIntegrationService integrationService,
            IEncryptionService encryptionService)
        {
            _gitWebhookService = gitWebhookService;
            _integrationService = integrationService;
            _encryptionService = encryptionService;
        }

        [HttpPost("{webhookToken}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> HandleWebhookAsync(string webhookToken)
        {
            // Request body újraolvasásának engedélyezése
            Request.EnableBuffering();

            //Payload kiolvasása
            using var reader = new StreamReader(Request.Body, leaveOpen: true);
            var payload = await reader.ReadToEndAsync();
            Request.Body.Position = 0;

            //Token alapján integráció keresése
            var integration = await _integrationService.GetByWebhookTokenAsync(webhookToken);
            if (integration == null)
                return Unauthorized("Érvénytelen webhook token!");

            //Provider alapján validáció
            if (integration.Provider == "GitHub")
            {
                var signature = Request.Headers["X-Hub-Signature-256"].ToString();
                if (string.IsNullOrEmpty(signature))
                    return Unauthorized("Hiányzó GitHub signature!");

                var decryptedSecret = _encryptionService.Decrypt(integration.WebhookSecret);
                if (!_gitWebhookService.ValidateGitHubSignature(payload, signature, decryptedSecret))
                    return Unauthorized("Érvénytelen GitHub signature!");
            }
            else if (integration.Provider == "GitLab")
            {
                var token = Request.Headers["X-Gitlab-Token"].ToString();
                if (string.IsNullOrEmpty(token))
                    return Unauthorized("Hiányzó GitLab token!");

                var decryptedSecret = _encryptionService.Decrypt(integration.WebhookSecret);
                if (!_gitWebhookService.ValidateGitLabSignature(token, decryptedSecret))
                    return Unauthorized("Érvénytelen GitLab token!");
            }

            //Event típus meghatározása
            var gitHubEvent = Request.Headers["X-GitHub-Event"].ToString();
            var gitLabEvent = Request.Headers["X-Gitlab-Event"].ToString();

            var payloadJson = JsonDocument.Parse(payload).RootElement;

            try
            {
                if (integration.Provider == "GitHub")
                {
                    switch (gitHubEvent)
                    {
                        case "ping":
                            await _integrationService.VerifyIntegrationAsync(integration.Id);
                            return Ok("pong");
                        case "push":
                            await _gitWebhookService.ProcessPushEventAsync(
                                integration.ProjectId, integration.Id, payloadJson);
                            break;
                        case "pull_request":
                            await _gitWebhookService.ProcessPullRequestEventAsync(
                                integration.ProjectId, integration.Id, payloadJson);
                            break;
                        default:
                            //Ismeretlen event - ignoráljuk
                            return Ok("Event ignored");
                    }
                }
                else if (integration.Provider == "GitLab")
                {
                    switch (gitLabEvent)
                    {
                        case "Push Hook":
                            await _gitWebhookService.ProcessPushEventAsync(
                                integration.ProjectId, integration.Id, payloadJson);
                            break;
                        case "Merge Request Hook":
                            await _gitWebhookService.ProcessPullRequestEventAsync(
                                integration.ProjectId, integration.Id, payloadJson);
                            break;
                        default:
                            return Ok("Event ignored");
                    }
                }

                return Ok("Webhook processed!");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}