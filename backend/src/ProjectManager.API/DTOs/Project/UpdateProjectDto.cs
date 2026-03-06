using System.ComponentModel.DataAnnotations;

namespace ProjectManager.API.DTOs.Project
{
    public class UpdateProjectDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool? IsArchived { get; set; }
    }
}
