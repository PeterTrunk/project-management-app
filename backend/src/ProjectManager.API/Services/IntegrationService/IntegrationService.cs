using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pipelines.Sockets.Unofficial.Arenas;
using ProjectManager.API.Common.Options;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.Integration;
using ProjectManager.API.Hubs;
using ProjectManager.API.Model;
using ProjectManager.API.Services.ActivityService;
using ProjectManager.API.Services.CurrentUserService;
using ProjectManager.API.Services.EncryptionService;

namespace ProjectManager.API.Services.IntegrationService
{
    public class IntegrationService : IIntegrationService
    {
        private readonly AppDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IActivityService _activityService;
        private readonly IHubContext<ProjectHub> _hubContext;
        private readonly IEncryptionService _encryptionService;
        private readonly ApiOptions _apiOptions;
        private readonly ILogger<IntegrationService> _logger;

        public IntegrationService(
            AppDbContext context, 
            ICurrentUserService currentUserService, 
            IActivityService activityService, 
            IHubContext<ProjectHub> hubContext,
            IEncryptionService encryptionService,
            IOptions<ApiOptions> apiOptions,
            ILogger<IntegrationService> logger)
        {
            _context = context;
            _currentUserService = currentUserService;
            _activityService = activityService;
            _hubContext = hubContext;
            _encryptionService = encryptionService;
            _apiOptions = apiOptions.Value;
            _logger = logger;
        }

        public async Task<IntegrationResponseDto> CreateIntegrationAsync(Guid projectId, CreateIntegrationDto dto)
        {
            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található!");
            
            var existing = await _context.Integrations
                .FirstOrDefaultAsync(i =>
                    i.ProjectId == projectId &&
                    i.Provider == dto.Provider &&
                    i.RepoFullName == dto.RepoFullName);
            if (existing != null)
                throw new Exception("Ez az integráció már létezik!");

            var integration = new Integration
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Provider = dto.Provider,
                RepoFullName = dto.RepoFullName,
                AccessToken = dto.AccessToken,
                WebhookSecret = _encryptionService.Encrypt(dto.WebhookSecret),
                WebhookToken = Guid.NewGuid().ToString("N"),
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.Integrations.AddAsync(integration);
            await _context.SaveChangesAsync();

            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("IntegrationCreated", new
                    {
                        integrationId = integration.Id,
                        provider = integration.Provider,
                        repoFullName = integration.RepoFullName,
                        isEnabled = integration.IsEnabled,
                        isVerified = integration.IsVerified,
                        webhookUrl = $"{_apiOptions.BaseUrl}/api/git/webhook/{integration.WebhookToken}",
                        createdAt = integration.CreatedAt
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "IntegrationCreated", projectId);
            }
            

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    "Integration",
                    integration.Id,
                    "Created",
                    $"{_currentUserService.DisplayName} hozzáadta a {integration.Provider} integrációt: {integration.RepoFullName}"
                );
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
            }

            return MapToDto(integration);
        }

        public async Task DeleteIntegrationAsync(Guid projectId, Guid integrationId)
        {
            var integration = await _context.Integrations
                .FirstOrDefaultAsync(i => i.Id == integrationId && i.ProjectId == projectId);
            if (integration == null)
                throw new Exception("Integráció nem található!");

            _context.Integrations.Remove(integration);
            await _context.SaveChangesAsync();

            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("IntegrationDeleted", new
                    {
                        integrationId = integration.Id
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "IntegrationDeleted", projectId);
            }

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    "Integration",
                    integration.Id,
                    "Deleted",
                    $"{_currentUserService.DisplayName} törölte a {integration.Provider} integrációt: {integration.RepoFullName}"
                );
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
            }
        }

        public async Task EnableDisableIntegrationAsync(Guid projectId, Guid integrationId, bool isEnabled)
        {
            var integration = await _context.Integrations
                .FirstOrDefaultAsync(i => i.Id == integrationId && i.ProjectId == projectId);
            if (integration == null)
                throw new Exception("Integráció nem található!");

            integration.IsEnabled = isEnabled;
            integration.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("IntegrationUpdated", new
                    {
                        integrationId = integration.Id,
                        isEnabled = integration.IsEnabled
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "IntegrationUpdated", projectId);
            }

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    "Integration",
                    integration.Id,
                    integration.IsEnabled ? "Enabled" : "Disabled",
                    $"{_currentUserService.DisplayName} {(integration.IsEnabled ? "engedélyezte" : "letiltotta")} a {integration.Provider} integrációt: {integration.RepoFullName}"
                );
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
            }
        }

        public async Task<Integration?> GetByWebhookTokenAsync(string webhookToken)
        {
            return await _context.Integrations
                .Include(i => i.Project)
                .FirstOrDefaultAsync(i => i.WebhookToken == webhookToken && i.IsEnabled);
        }

        public async Task<List<IntegrationResponseDto>> GetIntegrationsAsync(Guid projectId)
        {
            var integrations = await _context.Integrations
                .Where(i => i.ProjectId == projectId)
                .OrderBy(i => i.CreatedAt)
                .ToListAsync();

            return integrations.Select(i => MapToDto(i)).ToList();
        }

        public async Task<IntegrationResponseDto> RegenerateWebhookTokenAsync(Guid projectId, Guid integrationId)
        {
            var integration = await _context.Integrations
                .FirstOrDefaultAsync(i => i.Id == integrationId && i.ProjectId == projectId);
            if (integration == null)
                throw new Exception("Integráció nem található!");

            integration.WebhookToken = Guid.NewGuid().ToString("N");
            integration.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("IntegrationUpdated", new
                    {
                        integrationId = integration.Id,
                        isVerified = integration.IsVerified
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "IntegrationUpdated", projectId);
            }
            

            try
            {
                var activity = await _activityService.LogActivityAsync(
                    projectId,
                    "Integration",
                    integration.Id,
                    "TokenRegenerated",
                    $"{_currentUserService.DisplayName} regenerálta a webhook tokent: {integration.RepoFullName}"
                );
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", projectId);
            }

            return MapToDto(integration);
        }

        public async Task ResetWebhookSecretAsync(Guid projectId, Guid integrationId, string newSecret)
        {
            var integration = await _context.Integrations
                .FirstOrDefaultAsync(i => i.Id == integrationId && i.ProjectId == projectId);
            if (integration == null)
                throw new Exception("Integráció nem található");

            integration.WebhookSecret = _encryptionService.Encrypt(newSecret);
            integration.IsVerified = false;
            await _context.SaveChangesAsync();

            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("IntegrationUpdated", new
                    {
                        integrationId = integration.Id,
                        isVerified = false
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "IntegrationUpdated", projectId);
            }
        }

        public async Task VerifyIntegrationAsync(Guid integrationId)
        {
            var integration = await _context.Integrations
                .FirstOrDefaultAsync(i => i.Id == integrationId);
            if (integration == null)
                throw new Exception("Integráció nem található!");

            integration.IsVerified = true;
            integration.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            try
            {
                await _hubContext.Clients
                    .Group($"project-{integration.ProjectId}")
                    .SendAsync("IntegrationVerified", new
                    {
                        integrationId = integration.Id,
                        projectId = integration.ProjectId
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "IntegrationVerified", integration.ProjectId);
            }
            

            try
            {
                var activity = await _activityService.LogSystemActivityAsync(
                    integration.ProjectId,
                    "Integration",
                    integration.Id,
                    "Verified",
                    $"GitHub webhook sikeresen verifikálva: {integration.RepoFullName}"
                );
                await _hubContext.Clients
                    .Group($"project-{integration.ProjectId}")
                    .SendAsync("ActivityCreated", activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: ActivityCreated | ProjectId: {ProjectId}", integration.ProjectId);
            }
        }

        private IntegrationResponseDto MapToDto(Integration integration)
        {
            return new IntegrationResponseDto
            {
                Id = integration.Id,
                Provider = integration.Provider,
                RepoFullName = integration.RepoFullName,
                WebhookToken = integration.WebhookToken,
                WebhookUrl = $"{_apiOptions.BaseUrl}/api/git/webhook/{integration.WebhookToken}",
                IsEnabled = integration.IsEnabled,
                IsVerified = integration.IsVerified,
                HasAccessToken = !string.IsNullOrEmpty(integration.AccessToken),
                CreatedAt = integration.CreatedAt,
                UpdatedAt = integration.UpdatedAt
            };
        }
    }
}
