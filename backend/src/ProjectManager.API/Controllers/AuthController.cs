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
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var response = await _authService.RegisterAsync(dto, ip);
            SetRefreshTokenCookie(response.RefreshToken!);
            response.RefreshToken = null!;
            return Created(string.Empty, response);
        }

        [HttpPost("login")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<AuthResponseDto>> LoginAsync([FromBody] LoginDto dto)
        {
            var response = await _authService.LoginAsync(dto);
            if (!response.RequiresTotp)
            {
                SetRefreshTokenCookie(response.RefreshToken!, response.RememberMe);
                response.RefreshToken = null!;
            }
            return Ok(response);
        }

        [HttpPost("refresh")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<AuthResponseDto>> RefreshAsync()
        {
            var refreshToken = Request.Cookies["refreshToken"];
            if (string.IsNullOrEmpty(refreshToken))
                return BadRequest("Refresh token hiányzik!");

            var response = await _authService.RefreshTokenAsync(refreshToken);
            SetRefreshTokenCookie(response.RefreshToken!, response.RememberMe);
            response.RefreshToken = null!;
            return Ok(response);
        }

        [HttpPost("logout")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> LogoutAsync()
        {
            var refreshToken = Request.Cookies["refreshToken"];
            if (!string.IsNullOrEmpty(refreshToken))
                await _authService.LogoutAsync(refreshToken);

            DeleteRefreshTokenCookie();
            return Ok();
        }

        [HttpGet("me")]
        [Authorize]
        [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<UserProfileDto>> MeAsync()
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var response = await _authService.MeAsync(userId);
            return Ok(response);
        }

        [HttpPost("changepassword")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> ChangePasswordAsync([FromBody] ChangePasswordDto dto)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _authService.ChangePasswordAsync(userId, dto);

            //A szerver minden refresh tokent visszavont,
            //így a böngészőben maradt süti már használhatatlan - takarítsuk el
            DeleteRefreshTokenCookie();
            return Ok();
        }

        [HttpPatch("profile")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<UserProfileDto>> ChangeUserProfileAsync([FromBody] UpdateProfileDto dto)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var response = await _authService.ChangeUserProfileAsync(userId, dto);
            return Ok(response);
        }

        //TOTP setup - QR kód generálás
        [HttpPost("totp/setup")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<TotpSetupResponseDto>> SetupTotp()
        {
            var result = await _authService.SetupTotpAsync();
            return Ok(result);
        }

        //TOTP verify és aktiválás
        [HttpPost("totp/verify")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> VerifyAndEnableTotp([FromBody] VerifyTotpDto dto)
        {
            var success = await _authService.VerifyAndEnableTotpAsync(dto.Token);
            if (!success)
                return BadRequest("Érvénytelen TOTP token!");

            DeleteRefreshTokenCookie();
            return Ok("2FA sikeresen aktiválva!");
        }

        //TOTP kikapcsolás
        [HttpPost("totp/disable")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> DisableTotp([FromBody] DisableTotpDto dto)
        {
            await _authService.DisableTotpAsync(dto);
            DeleteRefreshTokenCookie();
            return Ok("2FA kikapcsolva!");
        }

        //TOTP login - második lépés
        //Itt nem kell Authorize, mivel csak ez után kapja meg az engedélyt!
        [HttpPost("totp/login")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<AuthResponseDto>> LoginWithTotp([FromBody] LoginWithTotpDto dto)
        {
            var result = await _authService.LoginWithTotpAsync(dto);
            SetRefreshTokenCookie(result.RefreshToken!, result.RememberMe);
            result.RefreshToken = null!;
            return Ok(result);
        }

        [HttpGet("verify-email")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> VerifyEmail([FromQuery] string token)
        {
            await _authService.VerifyEmailAsync(token);
            return Ok("Email sikeresen megerősítve!");
        }

        [HttpPost("resend-verification")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> ResendVerification([FromBody] ResendVerificationDto dto)
        {
            //A válasz szándékosan egységes: a "Felhasználó nem található" és az "Az email
            //cím már van megerősítve" különbsége eddig felhasználó-enumerációt engedett.
            //Ugyanaz a minta, mint a forgot-password végponton.
            try
            {
                await _authService.ResendVerificationEmailAsync(dto.Email);
            }
            catch (NotFoundException)
            {
                //Nincs ilyen felhasználó - kifelé ugyanaz a válasz megy
            }
            catch (ValidationException)
            {
                //Már megerősített cím - kifelé ugyanaz a válasz megy
            }

            return Ok("Ha az email cím megerősítésre vár, elküldtük a megerősítő levelet!");
        }

        [HttpPost("forgot-password")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            await _authService.ForgotPasswordAsync(dto.Email);
            //Mindig OK-t adunk vissza biztonsági okokból (ne derüljön ki hogy létezik-e az email)
            return Ok("Ha az email cím regisztrált, küldtünk egy jelszó visszaállítási linket!");
        }

        [HttpPost("reset-password")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            await _authService.ResetPasswordAsync(dto.Token, dto.NewPassword);
            return Ok("Jelszó sikeresen megváltoztatva!");
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

        //A törlő Set-Cookie fejlécnek pontosan ugyanazokat az attribútumokat kell hordoznia,
        //mint a beállítónak - eltérő Domain vagy Path esetén a böngésző nem azonosítja a sütit, és az bent marad
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
