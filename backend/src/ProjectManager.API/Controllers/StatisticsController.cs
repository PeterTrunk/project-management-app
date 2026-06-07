using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManager.API.DTOs.Statistics;
using ProjectManager.API.Services.StatisticsService;

namespace ProjectManager.API.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId}/statistics")]
    public class StatisticsController : ControllerBase
    {
        private readonly IStatisticsService _statisticsService;

        public StatisticsController(IStatisticsService statisticsService)
        {
            _statisticsService = statisticsService;
        }
        
        [HttpGet("task-status")]
        [Authorize(Policy = "ProjectViewer")]
        [ProducesResponseType(typeof(List<TaskStatusDistributionDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<TaskStatusDistributionDto>>> GetTaskStatusDistributionAsync(Guid projectId, [FromQuery] Guid? sprintId = null)
        {
            try
            {
                var response = await _statisticsService.GetTaskStatusDistributionAsync(projectId, sprintId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        
        [HttpGet("burndown")]
        [Authorize(Policy = "ProjectViewer")]
        [ProducesResponseType(typeof(List<BurndownDataPointDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<BurndownDataPointDto>>> GetBurndownAsync(Guid projectId, [FromQuery] Guid sprintId)
        {
            try
            {
                var response = await _statisticsService.GetBurndownAsync(projectId, sprintId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        
        [HttpGet("workload")]
        [Authorize(Policy = "ProjectViewer")]
        [ProducesResponseType(typeof(List<WorkloadDataPointDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<WorkloadDataPointDto>>> GetWorkloadAsync(Guid projectId, [FromQuery] Guid? sprintId = null)
        {
            try
            {
                var response = await _statisticsService.GetWorkloadAsync(projectId, sprintId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        
        [HttpGet("velocity")]
        [Authorize(Policy = "ProjectViewer")]
        [ProducesResponseType(typeof(List<VelocityDataPointDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<VelocityDataPointDto>>> GetVelocityAsync(Guid projectId)
        {
            try
            {
                var response = await _statisticsService.GetVelocityAsync(projectId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        
        [HttpGet("cumulative-flow")]
        [Authorize(Policy = "ProjectViewer")]
        [ProducesResponseType(typeof(List<CumulativeFlowDataPointDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<CumulativeFlowDataPointDto>>> GetCumulativeFlowAsync(
            Guid projectId,
            [FromQuery] DateTime dateFrom,
            [FromQuery] DateTime dateTo,
            [FromQuery] Guid? boardId = null)
        {
            try
            {
                var response = await _statisticsService.GetCumulativeFlowAsync(
                    projectId, dateFrom, dateTo, boardId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
