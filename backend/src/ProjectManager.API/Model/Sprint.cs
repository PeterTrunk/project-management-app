using System.ComponentModel.DataAnnotations;

namespace ProjectManager.API.Model
{
    public class Sprint
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Goal { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string State { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public uint xmin { get; set; }

        public Project Project { get; set; } = null!;
        public ICollection<ProjectTask> ProjectTasks { get; set; } = new List<ProjectTask>();
    }
}
