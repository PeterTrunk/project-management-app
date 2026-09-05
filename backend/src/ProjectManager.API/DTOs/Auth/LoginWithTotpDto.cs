namespace ProjectManager.API.DTOs.Auth
{
    public class LoginWithTotpDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string TotpToken { get; set; } = string.Empty;
        public bool RememberMe { get; set; } = false;
    }
}
