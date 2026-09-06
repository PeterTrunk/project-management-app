using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectManager.API.Common.Constants;
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
        private readonly ILogger<GitController> _logger;

        public GitController(
            IGitService gitService,
            ILogger<GitController> logger)
        {
            _gitService = gitService;
            _logger = logger;
        }
        
        [HttpGet("unmatched-commits")]
        [Authorize(Policy = PolicyNames.ProjectViewer)]
        [ProducesResponseType(typeof(List<CommitLinkResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<CommitLinkResponseDto>>> GetUnmatchedCommitsAsync(Guid projectId)
        {
            var response = await _gitService.GetUnmatchedCommitsAsync(projectId);
            return Ok(response);
        }
        
        [HttpGet("unmatched-prs")]
        [Authorize(Policy = PolicyNames.ProjectViewer)]
        [ProducesResponseType(typeof(List<PrLinkResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<PrLinkResponseDto>>> GetUnmatchedPrsAsync(Guid projectId)
        {
            var response = await _gitService.GetUnmatchedPrsAsync(projectId);
            return Ok(response);
        }
        
        [HttpPost("commits/{commitId}/assign/{taskId}")]
        [Authorize(Policy = PolicyNames.ProjectMember)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> AssignCommitToTaskAsync(Guid projectId, Guid commitId, Guid taskId)
        {
            await _gitService.AssignCommitToTaskAsync(projectId, commitId, taskId);
            return NoContent();
        }
        
        [HttpPost("prs/{prId}/assign/{taskId}")]
        [Authorize(Policy = PolicyNames.ProjectMember)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> AssignPrToTaskAsync(Guid projectId, Guid prId, Guid taskId)
        {
            await _gitService.AssignPrToTaskAsync(projectId, prId, taskId);
            return NoContent();
        }
    }
}
