namespace ProjectManager.API.DTOs.Statistics
{
    public class BurndownDataPointDto
    {
        public DateTime Date { get; set; }
        public int RemainingTasks { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
    }
}
