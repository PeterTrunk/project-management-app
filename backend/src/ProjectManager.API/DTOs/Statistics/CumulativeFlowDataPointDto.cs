namespace ProjectManager.API.DTOs.Statistics
{
    public class CumulativeFlowDataPointDto
    {
        public DateTime Date { get; set; }
        public List<StatusCountDto> StatusCounts { get; set; } = new();
    }
}
