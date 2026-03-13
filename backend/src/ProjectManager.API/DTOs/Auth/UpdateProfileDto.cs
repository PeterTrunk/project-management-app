using System.ComponentModel.DataAnnotations;

namespace ProjectManager.API.DTOs.Auth
{
    public class UpdateProfileDto
    {
        [Required]
        [MaxLength(120)]
        public string DisplayName { get; set; } = string.Empty;
    }
}
