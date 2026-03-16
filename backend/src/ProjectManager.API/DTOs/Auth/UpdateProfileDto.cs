using System.ComponentModel.DataAnnotations;

namespace ProjectManager.API.DTOs.Auth
{
    public class UpdateProfileDto
    {
        public string DisplayName { get; set; } = string.Empty;
    }
}
