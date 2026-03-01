namespace ProjectManager.API.Model
{
    public class PrLink
    {
        public Guid Id { get; set; }
        public Guid? TaskId { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string RepoFullName { get; set; } = string.Empty ;
        public int PrNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public string State {  get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? MergedAt { get; set; }

        public ProjectTask? ProjectTask { get; set; }
    }
}
