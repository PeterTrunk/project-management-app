namespace ProjectManager.API.DTOs.Statistics
{
    public class VelocityDataPointDto
    {
        public string SprintName { get; set; } = string.Empty;
        public int CompletedTasks { get; set; }
        public DateTime? SprintEndDate { get; set; }
    }
}
