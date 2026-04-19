using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public TeamController(ITeamService teamService)
        {
            _teamService = teamService;
        }

        [HttpGet]
        [Authorize(Policy = "ProjectViewer")]
        [ProducesResponseType(typeof(List<ProjectMemberResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<ProjectMemberResponseDto>>> GetMembersAsync(Guid projectId)
        {
            try
            {
                var response = await _teamService.GetMembersAsync(projectId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{userId}")]
        [Authorize(Policy = "ProjectOwner")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> RemoveMemberAsync(Guid projectId, Guid userId)
        {
            try
            {
                await _teamService.RemoveMemberAsync(projectId, userId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPatch("{userId}/role")]
        [Authorize(Policy = "ProjectOwner")]
        [ProducesResponseType(typeof(ProjectMemberResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ProjectMemberResponseDto>> UpdateMemberRoleAsync(Guid projectId, Guid userId, [FromBody] UpdateMemberRoleDto dto)
        {
            try
            {
                var response = await _teamService.UpdateMemberRoleAsync(projectId, userId, dto);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("invite")]
        [Authorize(Policy = "ProjectAdmin")]
        [ProducesResponseType(typeof(InviteLinkResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<InviteLinkResponseDto>> GenerateInviteLinkAsync(Guid projectId, [FromBody] GenerateInviteLinkDto dto)
        {
            try
            {
                var response = await _teamService.GenerateInviteLinkAsync(projectId, dto);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
