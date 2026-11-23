using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManager.API.Common.Constants;
using ProjectManager.API.DTOs.Git;
using ProjectManager.API.Filters;
using ProjectManager.API.Services.GitBackfillService;

namespace ProjectManager.API.Controllers
{
    /// <summary>
    /// A hiányosan érkezett pushok pótlása.
    ///
    /// Külön vezérlő, és nem a GitController része, mert ez az egyetlen hely, ahol a rendszer
    /// KIFELÉ hív: saját kérésszám-korlátja és saját hibakezelése van.
    /// </summary>
    [ApiController]
    [ServiceFilter(typeof(ProjectNotArchivedFilter))]
    [Route("api/projects/{projectId}/git/backfills")]
    public class GitBackfillController : ControllerBase
    {
        private readonly IGitBackfillService _backfillService;

        public GitBackfillController(IGitBackfillService backfillService)
        {
            _backfillService = backfillService;
        }

        [HttpGet]
        [Authorize(Policy = PolicyNames.ProjectViewer)]
        [ProducesResponseType(typeof(List<CommitBackfillResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<CommitBackfillResponseDto>>> GetBackfillsAsync(Guid projectId)
        {
            return Ok(await _backfillService.GetBackfillsAsync(projectId));
        }

        /// <summary>
        /// A pótlás indítása. Nem adminisztrátori jog: a hiány pótlása ugyanaz a művelet, mint
        /// amit a webhook magától elvégzett volna, csak utólag - a hitelesítő adat beállítása
        /// marad adminisztrátori.
        /// </summary>
        [HttpPost("{backfillId}/run")]
        [Authorize(Policy = PolicyNames.ProjectMember)]
        [ProducesResponseType(typeof(CommitBackfillResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<ActionResult<CommitBackfillResponseDto>> RunBackfillAsync(Guid projectId, Guid backfillId)
        {
            return Ok(await _backfillService.RunBackfillAsync(projectId, backfillId));
        }
    }
}
