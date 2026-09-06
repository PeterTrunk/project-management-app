using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.DTOs.Team;
using ProjectManager.API.Filters;
using ProjectManager.API.Services.TeamService;

namespace ProjectManager.API.Controllers
{
    [ApiController]
    [ServiceFilter(typeof(ProjectNotArchivedFilter))]
    [Route("api/projects/{projectId}/members")]
    public class TeamController : ControllerBase
    {
        private readonly ITeamService _teamService;
        private readonly ILogger<TeamController> _logger;

        public TeamController(
            ITeamService teamService,
            ILogger<TeamController> logger)
        {
            _teamService = teamService;
            _logger = logger;
        }

        [HttpGet]
        [Authorize(Policy = PolicyNames.ProjectViewer)]
        [ProducesResponseType(typeof(List<ProjectMemberResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<ProjectMemberResponseDto>>> GetMembersAsync(Guid projectId)
        {
            var response = await _teamService.GetMembersAsync(projectId);
            return Ok(response);
        }

        [HttpDelete("{userId}")]
        [Authorize(Policy = PolicyNames.ProjectOwner)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> RemoveMemberAsync(Guid projectId, Guid userId)
        {
            await _teamService.RemoveMemberAsync(projectId, userId);
            return NoContent();
        }

        [HttpPatch("{userId}/role")]
        [Authorize(Policy = PolicyNames.ProjectOwner)]
        [ProducesResponseType(typeof(ProjectMemberResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ProjectMemberResponseDto>> UpdateMemberRoleAsync(Guid projectId, Guid userId, [FromBody] UpdateMemberRoleDto dto)
        {
            var response = await _teamService.UpdateMemberRoleAsync(projectId, userId, dto);
            return Ok(response);
        }

        [HttpPost("invite")]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(typeof(InviteLinkResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<InviteLinkResponseDto>> GenerateInviteLinkAsync(Guid projectId, [FromBody] GenerateInviteLinkDto dto)
        {
            var response = await _teamService.GenerateInviteLinkAsync(projectId, dto);
            return Ok(response);
        }

        [HttpGet("invites")]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(typeof(List<InviteLinkResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<InviteLinkResponseDto>>> GetInvitationsAsync(Guid projectId)
        {
            var response = await _teamService.GetInvitationsAsync(projectId);
            return Ok(response);
        }

        [HttpDelete("invites/{token}")]
        [Authorize(Policy = PolicyNames.ProjectAdmin)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> DeleteInvitationAsync(Guid projectId, string token)
        {
            await _teamService.DeleteInvitationAsync(projectId, token);
            return NoContent();
        }
    }
}
