namespace ProjectManager.API.DTOs.Boards
{
    public class BoardResponseDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsDefault { get; set; } = false;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
