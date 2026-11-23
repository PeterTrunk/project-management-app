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
        Task ResetWebhookSecretAsync(Guid projectId, Guid integrationId, string newSecret);

        /// <summary>
        /// A szolgáltató API-jához tartozó hozzáférési token beállítása vagy törlése.
        /// Üres érték esetén a tárolt token törlődik.
        /// </summary>
        Task SetAccessTokenAsync(Guid projectId, Guid integrationId, string? accessToken);

        /// <summary>
        /// Az automatikus commit-pótlás be- vagy kikapcsolása az integráción.
        /// </summary>
        Task SetAutoBackfillAsync(Guid projectId, Guid integrationId, bool isEnabled);

        /// <summary>
        /// A saját telepítésű szolgáltató kiszolgálójának címe. Üres érték esetén az integráció
        /// a szolgáltató felhős változatát használja.
        /// </summary>
        Task SetInstanceUrlAsync(Guid projectId, Guid integrationId, string? instanceUrl);
    }
}
