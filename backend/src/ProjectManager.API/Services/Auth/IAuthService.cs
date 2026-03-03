using ProjectManager.API.DTOs.Auth;

namespace ProjectManager.API.Services.Auth
{
    public interface IAuthService
    {
        Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
        Task<AuthResponseDto> LoginAsync(LoginDto dto);
    }
}
