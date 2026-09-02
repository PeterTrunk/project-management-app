using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.DTOs.Project;
using ProjectManager.API.Filters;
using ProjectManager.API.Services.ProjectService;

namespace ProjectManager.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProjectController : ControllerBase
    {
        private readonly IProjectService _projectservice;
        private readonly ILogger<ProjectController> _logger;

        public ProjectController(
            IProjectService projectservice,
            ILogger<ProjectController> logger)
        {
            _projectservice = projectservice;
            _logger = logger;
        }
        
        [HttpPost]
        [Authorize]
        [ProducesResponseType(typeof(ProjectResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ProjectResponseDto>> CreateProjectAsync([FromBody] CreateProjectDto dto)
        {
            try
            {
                var response = await _projectservice.CreateProjectAsync(dto);
                return Created(string.Empty ,response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hiba | {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Authorize]
        [ProducesResponseType(typeof(List<ProjectResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<ProjectResponseDto>>> GetProjectsAsync()
        {
            try
            {
                var response = await _projectservice.GetProjectsAsync();
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hiba | {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{projectId}")]
        [Authorize(Policy = PolicyNames.ProjectViewer)]
        [ProducesResponseType(typeof(ProjectResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ProjectResponseDto>> GetProjectById(Guid projectId)
        {
            try
            {
                var response = await _projectservice.GetProjectByIdAsync(projectId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hiba | {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{projectId}")]
        [ServiceFilter(typeof(ProjectNotArchivedFilter))]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(typeof(ProjectResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ProjectResponseDto>> UpdateProject(Guid projectId, [FromBody] UpdateProjectDto dto)
        {
            try
            {
                var response = await _projectservice.UpdateProjectAsync(projectId, dto);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hiba | {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
        }
        
        [HttpPatch("{projectId}/archive")]
        [Authorize(Policy = PolicyNames.ProjectOwner)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> ArchiveProject(Guid projectId)
        {
            try
            {
                await _projectservice.ArchiveProjectAsync(projectId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hiba | {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
        }

        [HttpPatch("{projectId}/unarchive")]
        [Authorize(Policy = PolicyNames.ProjectOwner)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> UnarchiveProject(Guid projectId)
        {
            try
            {
                await _projectservice.UnarchiveProjectAsync(projectId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hiba | {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{projectId}")]
        [Authorize(Policy = PolicyNames.ProjectOwner)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> DeleteProjectAsync(Guid projectId)
        {
            try
            {
                await _projectservice.DeleteProjectAsync(projectId);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hiba | {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
        }
    }
}
