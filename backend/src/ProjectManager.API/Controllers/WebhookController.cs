using Microsoft.AspNetCore.Mvc;
using ProjectManager.API.Services.EncryptionService;
using ProjectManager.API.Services.GitWebhookService;
using ProjectManager.API.Services.IntegrationService;
using ProjectManager.API.Services.RateLimit;
using System.Text.Json;

namespace ProjectManager.API.Controllers
{
    [ApiController]
    [Route("api/git/webhook")]
    public class WebhookController : ControllerBase
    {
        //A payload teljes egészében memóriába olvasódik, majd az EnableBuffering miatt
        //még egyszer pufferelődik - korlát nélkül ez memórianyomás mindkét replikán
        private const int MaxPayloadBytes = 1_000_000;

        private readonly IGitWebhookService _gitWebhookService;
        private readonly IIntegrationService _integrationService;
        private readonly IEncryptionService _encryptionService;
        private readonly IRateLimitService _rateLimitService;
        private readonly ILogger<WebhookController> _logger;

        public WebhookController(
            IGitWebhookService gitWebhookService,
            IIntegrationService integrationService,
            IEncryptionService encryptionService,
            IRateLimitService rateLimitService,
            ILogger<WebhookController> logger)
        {
            _gitWebhookService = gitWebhookService;
            _integrationService = integrationService;
            _encryptionService = encryptionService;
            _rateLimitService = rateLimitService;
            _logger = logger;
        }

        [HttpPost("{webhookToken}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        [RequestSizeLimit(MaxPayloadBytes)]
        public async Task<IActionResult> HandleWebhookAsync(string webhookToken)
        {
            //Ez publikus végpont: a rate limit az integráció-lekérdezés ELŐTT fut,
            //különben hitelesítés nélkül terhelhető az adatbázis
            var (isLimited, retryAfter) = await _rateLimitService
                .IsRateLimitedAsync($"webhook:{webhookToken}", 60, TimeSpan.FromMinutes(1));
            if (isLimited)
            {
                _logger.LogWarning("Rate limit elérve webhook végponton");
                Response.Headers.RetryAfter = retryAfter.ToString();
                return StatusCode(StatusCodes.Status429TooManyRequests, "Túl sok kérés!");
            }

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
            else
            {
                //Fail-closed: ismeretlen provider esetén NE fusson le a feldolgozás
                //aláírás-ellenőrzés nélkül
                _logger.LogWarning("Ismeretlen provider a webhook végponton | Provider: {Provider} | IntegrationId: {IntegrationId}",
                    integration.Provider, integration.Id);
                return Unauthorized("Ismeretlen provider!");
            }

            //Event típus meghatározása
            var gitHubEvent = Request.Headers["X-GitHub-Event"].ToString();
            var gitLabEvent = Request.Headers["X-Gitlab-Event"].ToString();

            JsonElement payloadJson;
            try
            {
                payloadJson = JsonDocument.Parse(payload).RootElement;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Hibás JSON a webhook payloadban | IntegrationId: {IntegrationId}", integration.Id);
                return BadRequest("Érvénytelen JSON payload!");
            }

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
    }
}