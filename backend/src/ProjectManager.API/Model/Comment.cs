namespace ProjectManager.API.Model
{
    public class Comment
    {
        public Guid Id { get; set; }
        public Guid TaskId { get; set; }
        public Guid UserId { get; set; }
        public string Body { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ProjectTask ProjectTask { get; set; } = null!;
        public User User { get; set; } = null!;
    }
}
