using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.DTOs.Statistics;
using ProjectManager.API.Services.StatisticsService;

namespace ProjectManager.API.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId}/statistics")]
    public class StatisticsController : ControllerBase
    {
        private readonly IStatisticsService _statisticsService;
        private readonly ILogger<StatisticsController> _logger;

        public StatisticsController(
            IStatisticsService statisticsService,
            ILogger<StatisticsController> logger)
        {
            _statisticsService = statisticsService;
            _logger = logger;
        }
        
        [HttpGet("task-status")]
        [Authorize(Policy = PolicyNames.ProjectViewer)]
        [ProducesResponseType(typeof(List<TaskStatusDistributionDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<TaskStatusDistributionDto>>> GetTaskStatusDistributionAsync(Guid projectId, [FromQuery] Guid? sprintId = null)
        {
            var response = await _statisticsService.GetTaskStatusDistributionAsync(projectId, sprintId);
            return Ok(response);
        }
        
        [HttpGet("burndown")]
        [Authorize(Policy = PolicyNames.ProjectViewer)]
        [ProducesResponseType(typeof(List<BurndownDataPointDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<BurndownDataPointDto>>> GetBurndownAsync(Guid projectId, [FromQuery] Guid sprintId)
        {
            var response = await _statisticsService.GetBurndownAsync(projectId, sprintId);
            return Ok(response);
        }
        
        [HttpGet("workload")]
        [Authorize(Policy = PolicyNames.ProjectViewer)]
        [ProducesResponseType(typeof(List<WorkloadDataPointDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<WorkloadDataPointDto>>> GetWorkloadAsync(Guid projectId, [FromQuery] Guid? sprintId = null)
        {
            var response = await _statisticsService.GetWorkloadAsync(projectId, sprintId);
            return Ok(response);
        }
        
        [HttpGet("velocity")]
        [Authorize(Policy = PolicyNames.ProjectViewer)]
        [ProducesResponseType(typeof(List<VelocityDataPointDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<VelocityDataPointDto>>> GetVelocityAsync(Guid projectId)
        {
            var response = await _statisticsService.GetVelocityAsync(projectId);
            return Ok(response);
        }
        
        [HttpGet("cumulative-flow")]
        [Authorize(Policy = PolicyNames.ProjectViewer)]
        [ProducesResponseType(typeof(List<CumulativeFlowDataPointDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<CumulativeFlowDataPointDto>>> GetCumulativeFlowAsync(
            Guid projectId,
            [FromQuery] DateTime dateFrom,
            [FromQuery] DateTime dateTo,
            [FromQuery] Guid? boardId = null)
        {
            var response = await _statisticsService.GetCumulativeFlowAsync(
                projectId, dateFrom, dateTo, boardId);
            return Ok(response);
        }
    }
}
