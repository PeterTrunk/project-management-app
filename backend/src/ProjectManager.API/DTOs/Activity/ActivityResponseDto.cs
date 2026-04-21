namespace ProjectManager.API.DTOs.Activity
{
    public class ActivityResponseDto
    {
        public Guid Id { get; set; }
        public string ActorName { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public Guid EntityId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Payload { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
