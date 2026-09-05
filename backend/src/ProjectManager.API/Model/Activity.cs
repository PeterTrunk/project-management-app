namespace ProjectManager.API.Model
{
    public class Activity
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? ActorId { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public Guid EntityId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Payload { get; set; } 
        public DateTime CreatedAt { get; set; }

        public Project Project { get; set; } = null!;
        public User? Actor { get; set; }
    }
}
