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

        public IntegrationController(IIntegrationService integrationService)
        {
            _integrationService = integrationService;
        }

        [HttpGet]
        [Authorize(Policy = PolicyNames.ProjectViewer)]
        [ProducesResponseType(typeof(List<IntegrationResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<IntegrationResponseDto>>> GetIntegrationsAsync(Guid projectId)
        {
            try
            {
                var response = await _integrationService.GetIntegrationsAsync(projectId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        
        [HttpPost]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(typeof(IntegrationResponseDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<IntegrationResponseDto>> CreateIntegrationAsync(Guid projectId, [FromBody] CreateIntegrationDto dto)
        {
            try
            {
                var response = await _integrationService.CreateIntegrationAsync(projectId, dto);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        
        [HttpDelete("{integrationId}")]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> DeleteIntegrationAsync(Guid projectId, Guid integrationId)
        {
            try
            {
                await _integrationService.DeleteIntegrationAsync(projectId, integrationId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        
        [HttpPost("{integrationId}/regenerate")]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(typeof(IntegrationResponseDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<IntegrationResponseDto>> RegenerateWebhookTokenAsync(Guid projectId, Guid integrationId)
        {
            try
            {
                var response = await _integrationService.RegenerateWebhookTokenAsync(projectId, integrationId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPatch("{integrationId}/toggle")]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> EnableDisableIntegrationAsync(Guid projectId, Guid integrationId, [FromQuery] bool isEnabled)
        {
            try
            {
                await _integrationService.EnableDisableIntegrationAsync(projectId, integrationId, isEnabled);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{integrationId}/reset-secret")]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> ResetWebhookSecretAsync(Guid projectId, Guid integrationId, [FromBody] ResetWebhookSecretDto dto)
        {
            try
            {
                await _integrationService.ResetWebhookSecretAsync(projectId, integrationId, dto.NewSecret);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
