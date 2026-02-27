namespace ProjectManager.API.Model
{
    public class TaskAssignment
    {
        public Guid TaskId { get; set; }
        public Guid UserId { get; set; }
        public DateTime AssignedAt { get; set; }

        public ProjectTask ProjectTask { get; set; } = null!;
        public User User { get; set; } = null!;
    }
}
