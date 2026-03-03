using System.ComponentModel.DataAnnotations;

namespace ProjectManager.API.DTOs.Auth
{
    public class ChangePasswordDto
    {
        [Required]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [MinLength(8, ErrorMessage = "Az új jelszó minimum 8 karakter legyen.")]
        public string NewPassword { get; set; } = string.Empty;
    }
}
