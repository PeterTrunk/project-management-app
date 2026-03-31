namespace ProjectManager.API.Model
{
    public class ProjectTask
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? BoardId { get; set; }
        public Guid? ColumnId { get; set; }
        public Guid? SprintId { get; set; }
        public Guid CreatedById { get; set; }
        public string TaskKey { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Priority { get; set; }
        public string Position { get; set; } = string.Empty;
        public int? EstimateInMinutes { get; set; } = 0;
        public DateTime? DueDate { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Project Project { get; set; } = null!;
        public Board Board { get; set; } = null!;
        public ColumnDefinition ColumnDefinition { get; set; } = null!;
        public Sprint Sprint { get; set; } = null!;
        public User CreatedByUser { get; set; } = null!;
        public ICollection<TaskAssignment> TaskAssignments { get; set; } = new List<TaskAssignment>();
        public ICollection<LabelTask> AssignedLabels { get; set; } = new List<LabelTask>();
        public ICollection<Comment> CommentsOnTask { get; set; } = new List<Comment>();
        public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
        public ICollection<CommitLink> CommitLinks { get; set; } = new List<CommitLink>();
        public ICollection<PrLink> PrLinks { get; set; } = new List<PrLink>();
    }
}
