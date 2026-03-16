using System.ComponentModel.DataAnnotations;

namespace ProjectManager.API.DTOs.Project
{
    public class CreateProjectDto
    {
        public string Name { get; set; } = string.Empty;
        public string ProjKey { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
