using System.ComponentModel.DataAnnotations;

namespace ProjectManager.API.DTOs.Labels
{
    public class CreateLabelDto
    {
        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(7)]
        public string Color { get; set; } = string.Empty;
    }
}
