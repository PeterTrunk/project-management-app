using System.ComponentModel.DataAnnotations;

namespace ProjectManager.API.DTOs.Task
{
    public class UpdateTaskDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public Guid? BoardId { get; set; }
        public Guid? SprintId { get; set; }
        public string? Priority { get; set; }
        public int? EstimateInMinutes { get; set; }
        public DateTime? DueDate { get; set; }
    }
}
