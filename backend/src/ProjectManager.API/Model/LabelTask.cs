namespace ProjectManager.API.Model
{
    public class LabelTask
    {
        public Guid TaskId { get; set; }
        public Guid LabelId { get; set; }

        public ProjectTask ProjectTask { get; set; } = null!;
        public Label Label { get; set; } = null!;
    }
}
