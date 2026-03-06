using System.ComponentModel.DataAnnotations;

namespace ProjectManager.API.DTOs.ProjectTask
{
    public class CreateTaskDto
    {
        [Required]
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid? BoardId { get; set; }
        public Guid? SprintId { get; set; }
        public string? Priority { get; set; }
        public int? EstimateInMinutes { get; set; }
        public DateTime? DueDate { get; set; }
    }
}
