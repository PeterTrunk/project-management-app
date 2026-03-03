using Microsoft.AspNetCore.Mvc;
using ProjectManager.API.DTOs.Auth;
using ProjectManager.API.Services.Auth;

namespace ProjectManager.API.Controllers
{
    //Attribútumok
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authservice;   

        public AuthController(IAuthService authservice)
        {
            _authservice = authservice;
        }

        [HttpPost("register")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<AuthResponseDto>> RegisterAsync([FromBody]  RegisterDto dto)
        {
            try
            {
                var response = await _authservice.RegisterAsync(dto);
                return Created(string.Empty, response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
            
        }

        [HttpPost("login")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<AuthResponseDto>> LoginAsync([FromBody] LoginDto dto)
        {
            try
            {
                var response = await _authservice.LoginAsync(dto);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

    }
}
