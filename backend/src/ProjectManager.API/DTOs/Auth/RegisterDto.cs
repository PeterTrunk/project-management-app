using System.ComponentModel.DataAnnotations;

namespace ProjectManager.API.DTOs.Auth
{
    public class RegisterDto
    {
        [Required]
        [EmailAddress]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(120, ErrorMessage = "A megjelenítési név maximum 120 karakter lehet.")]
        [MinLength(3, ErrorMessage = "A megjelenítési név minimum 3 karakter legyen.")]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [MinLength(8, ErrorMessage = "A jelszó minimum 8 karakter legyen.")]
        public string Password { get; set; } = string.Empty;
    }
}
