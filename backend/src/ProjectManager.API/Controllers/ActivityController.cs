using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.DTOs.Activity;
using ProjectManager.API.Services.ActivityService;

namespace ProjectManager.API.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId}/activities")]
    public class ActivityController : ControllerBase
    {
        private readonly IActivityService _activityService;
        private readonly ILogger<ActivityController> _logger;

        public ActivityController(
            IActivityService activityService,
            ILogger<ActivityController> logger)
        {
            _activityService = activityService;
            _logger = logger;
        }

        [HttpGet]
        [Authorize(Policy = PolicyNames.ProjectViewer)]
        [ProducesResponseType(typeof(List<ActivityResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<ActivityResponseDto>>> GetActivitiesAsync(
            Guid projectId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? entityType = null,
            [FromQuery] string? actorName = null,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null)
        {
            try
            {
                var response = await _activityService.GetActivitiesAsync(
                    projectId, page, pageSize, entityType, actorName, dateFrom, dateTo);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hiba | {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
        }
    }
}
