namespace ProjectManager.API.DTOs.Auth
{
    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string DisplayName {  get; set; } = string.Empty;
        public bool RequiresTotp { get; set; } = false;
        public bool IsTotpEnabled { get; set; } = false;
    }
}
