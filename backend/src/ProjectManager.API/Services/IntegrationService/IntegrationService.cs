using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.Integration;
using ProjectManager.API.Hubs;
using ProjectManager.API.Model;
using ProjectManager.API.Services.CurrentUserService;

namespace ProjectManager.API.Services.IntegrationService
{
    public class IntegrationService : IIntegrationService
    {
        private readonly AppDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IHubContext<ProjectHub> _hubContext;

        public IntegrationService(AppDbContext context, ICurrentUserService currentUserService, IHubContext<ProjectHub> hubContext)
        {
            _context = context;
            _currentUserService = currentUserService;
            _hubContext = hubContext;
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
                WebhookSecret = Environment.GetEnvironmentVariable("GIT_WEBHOOK_SECRET")
                    ?? throw new InvalidOperationException("GIT_WEBHOOK_SECRET nincs beállítva!"),
                WebhookToken = Guid.NewGuid().ToString("N"),
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.Integrations.AddAsync(integration);
            await _context.SaveChangesAsync();

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

            return MapToDto(integration);
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

            await _hubContext.Clients
                .Group($"project-{integration.ProjectId}")
                .SendAsync("IntegrationVerified", new
                {
                    integrationId = integration.Id,
                    projectId = integration.ProjectId
                });
        }

        private IntegrationResponseDto MapToDto(Integration integration)
        {
            var baseUrl = Environment.GetEnvironmentVariable("API_BASE_URL")
                ?? "http://localhost:5178";

            return new IntegrationResponseDto
            {
                Id = integration.Id,
                Provider = integration.Provider,
                RepoFullName = integration.RepoFullName,
                WebhookToken = integration.WebhookToken,
                WebhookUrl = $"{baseUrl}/api/git/webhook/{integration.WebhookToken}",
                IsEnabled = integration.IsEnabled,
                IsVerified = integration.IsVerified,
                HasAccessToken = !string.IsNullOrEmpty(integration.AccessToken),
                CreatedAt = integration.CreatedAt,
                UpdatedAt = integration.UpdatedAt
            };
        }
    }
}
