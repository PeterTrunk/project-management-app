using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectManager.API.DTOs.Git;
using ProjectManager.API.Filters;
using ProjectManager.API.Hubs;
using ProjectManager.API.Services.ActivityService;
using ProjectManager.API.Services.CurrentUserService;
using ProjectManager.API.Services.GitService;

namespace ProjectManager.API.Controllers
{
    [ApiController]
    [ServiceFilter(typeof(ProjectNotArchivedFilter))]
    [Route("api/projects/{projectId}/git")]
    public class GitController : ControllerBase
    {
        private readonly IGitService _gitService;
        

        public GitController(IGitService gitService)
        {
            _gitService = gitService;
        }
        
        [HttpGet("unmatched-commits")]
        [Authorize(Policy = "ProjectViewer")]
        [ProducesResponseType(typeof(List<CommitLinkResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<CommitLinkResponseDto>>> GetUnmatchedCommitsAsync(Guid projectId)
        {
            try
            {
                var response = await _gitService.GetUnmatchedCommitsAsync(projectId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        
        [HttpGet("unmatched-prs")]
        [Authorize(Policy = "ProjectViewer")]
        [ProducesResponseType(typeof(List<PrLinkResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<PrLinkResponseDto>>> GetUnmatchedPrsAsync(Guid projectId)
        {
            try
            {
                var response = await _gitService.GetUnmatchedPrsAsync(projectId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        
        [HttpPost("commits/{commitId}/assign/{taskId}")]
        [Authorize(Policy = "ProjectMember")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> AssignCommitToTaskAsync(Guid projectId, Guid commitId, Guid taskId)
        {
            try
            {
                await _gitService.AssignCommitToTaskAsync(projectId, commitId, taskId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        
        [HttpPost("prs/{prId}/assign/{taskId}")]
        [Authorize(Policy = "ProjectMember")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> AssignPrToTaskAsync(Guid projectId, Guid prId, Guid taskId)
        {
            try
            {
                await _gitService.AssignPrToTaskAsync(projectId, prId, taskId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
