using System.ComponentModel.DataAnnotations;

namespace ProjectManager.API.DTOs.Auth
{
    public class RefreshTokenDto
    {
        public string RefreshToken { get; set; } = string.Empty;
    }
}
