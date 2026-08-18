using ProjectManager.API.DTOs.Auth;

namespace ProjectManager.API.Services.Auth
{
    public interface IAuthService
    {
        Task<AuthResponseDto> RegisterAsync(RegisterDto dto, string ipAddress);
        Task<AuthResponseDto> LoginAsync(LoginDto dto);
        Task<AuthResponseDto> RefreshTokenAsync(string refreshToken);
        Task LogoutAsync(string refreshToken);
        Task<UserProfileDto> MeAsync(Guid userId);
        Task ChangePasswordAsync(Guid userId, ChangePasswordDto dto);
        Task <UserProfileDto> ChangeUserProfileAsync(Guid userId, UpdateProfileDto dto);

        //TOTP
        Task<TotpSetupResponseDto> SetupTotpAsync();
        Task<bool> VerifyAndEnableTotpAsync(string token);
        Task DisableTotpAsync();
        Task<AuthResponseDto> LoginWithTotpAsync(LoginWithTotpDto dto);
        Task<bool> IsTotpRequiredAsync(string email);
        Task VerifyEmailAsync(string token);
        Task ResendVerificationEmailAsync(string email);

        //Password reset
        Task ForgotPasswordAsync(string email);
        Task ResetPasswordAsync(string token, string newPassword);

    }
}
