using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManager.API.DTOs.Team;
using ProjectManager.API.Services.TeamService;

namespace ProjectManager.API.Controllers
{
    [ApiController]
    [Route("api/projects")]
    public class ProjectJoinController : ControllerBase
    {   
        private readonly ITeamService _teamService;
        private readonly ILogger<ProjectJoinController> _logger;

        public ProjectJoinController(
            ITeamService teamService,
            ILogger<ProjectJoinController> logger)
        {
            _teamService = teamService;
            _logger = logger;
        }
        
        [HttpPost("join/{token}")]
        [Authorize]
        [ProducesResponseType(typeof(ProjectMemberResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ProjectMemberResponseDto>> JoinProjectAsync(string token)
        {
            var response = await _teamService.JoinProjectAsync(token);
            return Ok(response);
        }
    }
}
