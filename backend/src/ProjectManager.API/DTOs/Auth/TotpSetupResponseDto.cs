namespace ProjectManager.API.DTOs.Auth
{
    public class TotpSetupResponseDto
    {
        public string SecretKey { get; set; } = string.Empty;
        public string OtpAuthUri { get; set; } = string.Empty;
    }
}
