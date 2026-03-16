using System.ComponentModel.DataAnnotations;

namespace ProjectManager.API.DTOs.Labels
{
    public class CreateLabelDto
    {
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
    }
}
