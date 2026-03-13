using ProjectManager.API.DTOs.Auth;

namespace ProjectManager.API.Services.Auth
{
    public interface IAuthService
    {
        Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
        Task<AuthResponseDto> LoginAsync(LoginDto dto);
        Task<AuthResponseDto> RefreshTokenAsync(string refreshToken);
        Task LogoutAsync(string refreshToken);
        Task<UserProfileDto> MeAsync(Guid userId);
        Task ChangePasswordAsync(Guid userId, ChangePasswordDto dto);
        Task <UserProfileDto> ChangeUserProfileAsync(Guid userId, UpdateProfileDto dto);
    }
}
