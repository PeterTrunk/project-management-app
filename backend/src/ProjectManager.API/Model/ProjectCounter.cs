namespace ProjectManager.API.Model
{
    public class ProjectCounter
    {
        public Guid ProjectId { get; set; }
        public long LastNum { get; set; }

        public Project Project { get; set; } = null!;
    }
}
