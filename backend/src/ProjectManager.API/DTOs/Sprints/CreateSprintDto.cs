namespace ProjectManager.API.DTOs.Sprints
{
    public class CreateSprintDto
    {
        public Guid ProjectId { get; set; }
        public Guid? BoardId { get; set; }
        /// <summary>
        /// Sprint neve, maximum 80 karakter
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// Sprint cél leírása, maximum 500 karakter
        /// </summary>
        public string? Goal { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        /// <summary>
        /// Sprint státusza, State-ek: "Planned","Active","Planned"
        /// </summary>
        public string State { get; set; } = string.Empty;
    }
}
