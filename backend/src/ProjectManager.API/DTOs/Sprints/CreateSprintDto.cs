namespace ProjectManager.API.DTOs.Sprints
{
    public class CreateSprintDto
    {
        public Guid ProjectId { get; set; }
        public Guid? BoardId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Goal { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string State { get; set; } = string.Empty;
    }
}
