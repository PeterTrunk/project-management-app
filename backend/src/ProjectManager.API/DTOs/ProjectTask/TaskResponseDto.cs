namespace ProjectManager.API.DTOs.Task
{
    public class TaskResponseDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? BoardId { get; set; }
        public Guid? ColumnId { get; set; }
        public Guid? SprintId { get; set; }
        public List<string> AssigneeNames { get; set; } = new List<string>();
        public List<string> LabelNames { get; set; } = new List<string>();
        public List<string> CommitLinks { get; set; } = new List<string>();
        public List<string> PrLinks { get; set; } = new List<string>();
        public string CreatedByName { get; set; } = string.Empty;
        public string TaskKey { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Priority { get; set; }
        public float Position { get; set; }
        public int? EstimateInMinutes { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

    }
}
