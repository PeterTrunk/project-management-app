using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManager.API.Common.Exceptions;
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
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IAuthService authservice,
            ILogger<AuthController> logger)
        {
            _authService = authservice;
            _logger = logger;
        }

        [HttpPost("register")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<AuthResponseDto>> RegisterAsync([FromBody]  RegisterDto dto)
        {
            try
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var response = await _authService.RegisterAsync(dto, ip);
                SetRefreshTokenCookie(response.RefreshToken!);
                response.RefreshToken = null!;
                return Created(string.Empty, response);
            }
            catch (RateLimitException ex)
            {
                _logger.LogError(ex, "Hiba | {Message}", ex.Message);
                return StatusCode(429, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hiba | {Message}", ex.Message);
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
                if (!response.RequiresTotp)
                {
                    SetRefreshTokenCookie(response.RefreshToken!, response.RememberMe);
                    response.RefreshToken = null!;
                }
                return Ok(response);
            }
            catch (RateLimitException ex)
            {
                _logger.LogError(ex, "Hiba | {Message}", ex.Message);
                return StatusCode(429, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hiba | {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("refresh")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<AuthResponseDto>> RefreshAsync()
        {
            try
            {
                var refreshToken = Request.Cookies["refreshToken"];
                if (string.IsNullOrEmpty(refreshToken))
                    return BadRequest("Refresh token hiányzik!");

                var response = await _authService.RefreshTokenAsync(refreshToken);
                SetRefreshTokenCookie(response.RefreshToken!, response.RememberMe);
                response.RefreshToken = null!;
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hiba | {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("logout")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> LogoutAsync()
        {
            try
            {
                var refreshToken = Request.Cookies["refreshToken"];
                if (!string.IsNullOrEmpty(refreshToken))
                    await _authService.LogoutAsync(refreshToken);

                DeleteRefreshTokenCookie();
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hiba | {Message}", ex.Message);
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
                _logger.LogError(ex, "Hiba | {Message}", ex.Message);
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

                //A szerver minden refresh tokent visszavont, így a böngészőben maradt
                //süti már használhatatlan - takarítsuk el
                DeleteRefreshTokenCookie();
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hiba | {Message}", ex.Message);
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
                _logger.LogError(ex, "Hiba | {Message}", ex.Message);
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
                _logger.LogError(ex, "Hiba | {Message}", ex.Message);
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

                DeleteRefreshTokenCookie();
                return Ok("2FA sikeresen aktiválva!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hiba | {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
        }

        //TOTP kikapcsolás
        [HttpPost("totp/disable")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> DisableTotp([FromBody] DisableTotpDto dto)
        {
            try
            {
                await _authService.DisableTotpAsync(dto);
                DeleteRefreshTokenCookie();
                return Ok("2FA kikapcsolva!");
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Sikertelen 2FA kikapcsolás | {Message}", ex.Message);
                return Unauthorized(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hiba | {Message}", ex.Message);
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
                SetRefreshTokenCookie(result.RefreshToken!, result.RememberMe);
                result.RefreshToken = null!;
                return Ok(result);
            }
            catch (RateLimitException ex)
            {
                _logger.LogError(ex, "Hiba | {Message}", ex.Message);
                return StatusCode(429, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hiba | {Message}", ex.Message);
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
                _logger.LogError(ex, "Hiba | {Message}", ex.Message);
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
            catch (RateLimitException ex)
            {
                _logger.LogWarning("Rate limit | {Message}", ex.Message);
                return StatusCode(429, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hiba | {Message}", ex.Message);
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
            catch (RateLimitException ex)
            {
                _logger.LogError(ex, "Hiba | {Message}", ex.Message);
                return StatusCode(429, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hiba | {Message}", ex.Message);
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
                _logger.LogError(ex, "Hiba | {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
        }

        private void SetRefreshTokenCookie(string refreshToken, bool rememberMe = false)
        {
            var isProd = HttpContext.RequestServices
                .GetRequiredService<IWebHostEnvironment>()
                .IsProduction();

            Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = isProd,
                SameSite = isProd ? SameSiteMode.Strict : SameSiteMode.Lax,
                Domain = isProd ? ".trunkpeter.com" : null,
                Path = "/api/auth",
                Expires = rememberMe ? DateTime.UtcNow.AddDays(30) : null
            });
        }

        //A törlő Set-Cookie fejlécnek pontosan ugyanazokat az attribútumokat kell
        //hordoznia, mint a beállítónak - eltérő Domain vagy Path esetén a böngésző
        //nem azonosítja a sütit, és az bent marad
        private void DeleteRefreshTokenCookie()
        {
            var isProd = HttpContext.RequestServices
                .GetRequiredService<IWebHostEnvironment>()
                .IsProduction();

            Response.Cookies.Delete("refreshToken", new CookieOptions
            {
                HttpOnly = true,
                Secure = isProd,
                SameSite = isProd ? SameSiteMode.Strict : SameSiteMode.Lax,
                Domain = isProd ? ".trunkpeter.com" : null,
                Path = "/api/auth"
            });
        }
    }
}
