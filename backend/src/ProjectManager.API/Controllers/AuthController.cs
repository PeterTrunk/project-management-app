using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManager.API.DTOs.Auth;
using ProjectManager.API.Services.Auth;
using System.Security.Claims;

namespace ProjectManager.API.Controllers
{
    //Attribútumok
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        
        
        public AuthController(IAuthService authservice)
        {
            _authService = authservice;
        }

        [HttpPost("register")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<AuthResponseDto>> RegisterAsync([FromBody]  RegisterDto dto)
        {
            try
            {
                var response = await _authService.RegisterAsync(dto);
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
                var response = await _authService.LoginAsync(dto);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("refresh")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<AuthResponseDto>> RefreshAsync([FromBody] RefreshTokenDto dto)
        {
            try
            {

                var response = await _authService.RefreshTokenAsync(dto.RefreshToken);
                return Ok(response);
            }
            catch (Exception ex)
            {

                return BadRequest(ex.Message);
            }
        }

        [HttpPost("logout")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> LogoutAsync([FromBody] RefreshTokenDto dto)
        {
            try
            {

                await _authService.LogoutAsync(dto.RefreshToken);
                return Ok();
            }
            catch (Exception ex)
            {

                return BadRequest(ex.Message);
            }
        }

        [HttpGet("me")]
        [Authorize]
        [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<UserProfileDto>> MeAsync()
        {
            try
            {
                var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var response = await _authService.MeAsync(userId);
                return Ok(response);
            }
            catch (Exception ex)
            {

                return BadRequest(ex.Message);
            }
        }

        [HttpPost("changepassword")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> ChangePasswordAsync([FromBody] ChangePasswordDto dto)
        {
            try
            {
                var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await _authService.ChangePasswordAsync(userId, dto);
                return Ok();
            }
            catch (Exception ex)
            {

                return BadRequest(ex.Message);
            }
        }

        [HttpPatch("profile")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<UserProfileDto>> ChangeUserProfileAsync([FromBody] UpdateProfileDto dto)
        {
            try
            {
                var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var response = await _authService.ChangeUserProfileAsync(userId, dto);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        //TOTP setup - QR kód generálás
        [HttpPost("totp/setup")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<TotpSetupResponseDto>> SetupTotp()
        {
            try
            {
                var result = await _authService.SetupTotpAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        //TOTP verify és aktiválás
        [HttpPost("totp/verify")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> VerifyAndEnableTotp([FromBody] VerifyTotpDto dto)
        {
            try
            {
                var success = await _authService.VerifyAndEnableTotpAsync(dto.Token);
                if (!success)
                    return BadRequest("Érvénytelen TOTP token!");
                return Ok("2FA sikeresen aktiválva!");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        //TOTP kikapcsolás
        [HttpPost("totp/disable")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> DisableTotp()
        {
            try
            {
                await _authService.DisableTotpAsync();
                return Ok("2FA kikapcsolva!");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        //TOTP login - második lépés
        //Itt nem kell Authorize, mivel csak ez után kapja meg az engedélyt!
        [HttpPost("totp/login")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<AuthResponseDto>> LoginWithTotp([FromBody] LoginWithTotpDto dto)
        {
            try
            {
                var result = await _authService.LoginWithTotpAsync(dto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("verify-email")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> VerifyEmail([FromQuery] string token)
        {
            try
            {
                await _authService.VerifyEmailAsync(token);
                return Ok("Email sikeresen megerősítve!");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("resend-verification")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> ResendVerification([FromBody] ResendVerificationDto dto)
        {
            try
            {
                await _authService.ResendVerificationEmailAsync(dto.Email);
                return Ok("Megerősítő email elküldve!");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("forgot-password")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            try
            {
                await _authService.ForgotPasswordAsync(dto.Email);
                // Mindig OK-t adunk vissza biztonsági okokból (ne derüljön ki hogy létezik-e az email)
                return Ok("Ha az email cím regisztrált, küldtünk egy jelszó visszaállítási linket!");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("reset-password")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            try
            {
                await _authService.ResetPasswordAsync(dto.Token, dto.NewPassword);
                return Ok("Jelszó sikeresen megváltoztatva!");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
