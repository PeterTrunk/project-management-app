using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.DTOs.Integration;
using ProjectManager.API.Filters;
using ProjectManager.API.Services.IntegrationService;

namespace ProjectManager.API.Controllers
{
    [ApiController]
    [ServiceFilter(typeof(ProjectNotArchivedFilter))]
    [Route("api/projects/{projectId}/integrations")]
    public class IntegrationController : ControllerBase
    {
        private readonly IIntegrationService _integrationService;
        private readonly ILogger<IntegrationController> _logger;

        public IntegrationController(
            IIntegrationService integrationService,
            ILogger<IntegrationController> logger)
        {
            _integrationService = integrationService;
            _logger = logger;
        }

        [HttpGet]
        [Authorize(Policy = PolicyNames.ProjectViewer)]
        [ProducesResponseType(typeof(List<IntegrationResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<IntegrationResponseDto>>> GetIntegrationsAsync(Guid projectId)
        {
            var response = await _integrationService.GetIntegrationsAsync(projectId);
            return Ok(response);
        }
        
        [HttpPost]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(typeof(IntegrationResponseDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<IntegrationResponseDto>> CreateIntegrationAsync(Guid projectId, [FromBody] CreateIntegrationDto dto)
        {
            var response = await _integrationService.CreateIntegrationAsync(projectId, dto);
            return Ok(response);
        }
        
        [HttpDelete("{integrationId}")]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> DeleteIntegrationAsync(Guid projectId, Guid integrationId)
        {
            await _integrationService.DeleteIntegrationAsync(projectId, integrationId);
            return NoContent();
        }
        
        [HttpPost("{integrationId}/regenerate")]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(typeof(IntegrationResponseDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<IntegrationResponseDto>> RegenerateWebhookTokenAsync(Guid projectId, Guid integrationId)
        {
            var response = await _integrationService.RegenerateWebhookTokenAsync(projectId, integrationId);
            return Ok(response);
        }

        [HttpPatch("{integrationId}/toggle")]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> EnableDisableIntegrationAsync(Guid projectId, Guid integrationId, [FromQuery] bool isEnabled)
        {
            await _integrationService.EnableDisableIntegrationAsync(projectId, integrationId, isEnabled);
            return NoContent();
        }

        [HttpPost("{integrationId}/reset-secret")]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> ResetWebhookSecretAsync(Guid projectId, Guid integrationId, [FromBody] ResetWebhookSecretDto dto)
        {
            await _integrationService.ResetWebhookSecretAsync(projectId, integrationId, dto.NewSecret);
            return NoContent();
        }
    }
}
