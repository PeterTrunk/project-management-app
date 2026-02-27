namespace ProjectManager.API.Model
{
    public class Board
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsDefault { get; set; } = false;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Project Project { get; set; } = null!;
        public ICollection<ColumnDefinition> ColumnDefinitions { get; set; } = new List<ColumnDefinition>();
        public ICollection<Sprint> Sprints { get; set; } = new List<Sprint>();
        public ICollection<ProjectTask> ProjectTasks { get; set; } = new List<ProjectTask>();
    }
}
