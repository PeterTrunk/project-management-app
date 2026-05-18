namespace ProjectManager.API.Model
{
    public class TaskStatusHistory
    {
        public Guid Id { get; set; }
        public Guid TaskId { get; set; }
        public Guid? ColumnId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public ProjectTask Task { get; set; } = null!;
        public ColumnDefinition? Column { get; set; }
    }
}
