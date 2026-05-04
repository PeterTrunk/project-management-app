using ProjectManager.API.DTOs.Integration;
using ProjectManager.API.Model;

namespace ProjectManager.API.Services.IntegrationService
{
    public interface IIntegrationService
    {
        Task<List<IntegrationResponseDto>> GetIntegrationsAsync(Guid projectId);
        Task<IntegrationResponseDto> CreateIntegrationAsync(Guid projectId, CreateIntegrationDto dto);
        Task DeleteIntegrationAsync(Guid projectId, Guid integrationId);
        Task<IntegrationResponseDto> RegenerateWebhookTokenAsync(Guid projectId, Guid integrationId);
        Task<Integration?> GetByWebhookTokenAsync(string webhookToken);
        Task EnableDisableIntegrationAsync(Guid projectId, Guid integrationId, bool isEnabled);
        Task VerifyIntegrationAsync(Guid integrationId);
    }
}
