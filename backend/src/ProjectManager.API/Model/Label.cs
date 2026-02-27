namespace ProjectManager.API.Model
{
    public class Label
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;

        public Project Project { get; set; } = null!;
        public ICollection<LabelTask> LabelTasks { get; set; } = new List<LabelTask>();
    }
}
