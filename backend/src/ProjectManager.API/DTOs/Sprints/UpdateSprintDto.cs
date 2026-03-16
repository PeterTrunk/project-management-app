namespace ProjectManager.API.DTOs.Sprints
{
    public class UpdateSprintDto
    {
        public Guid? BoardId { get; set; }
        public string? Name { get; set; }
        public string? Goal { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
