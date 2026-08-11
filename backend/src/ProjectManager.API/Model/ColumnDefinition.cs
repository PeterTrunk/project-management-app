using System.ComponentModel.DataAnnotations;

namespace ProjectManager.API.Model
{
    public class ColumnDefinition
    {
        public Guid Id { get; set; }
        public Guid BoardId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string MapsToStatus {  get; set; } = string.Empty;
        public int? WipLimit { get; set; }
        public int Position {  get; set; }
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = null!;

        public Board Board { get; set; } = null!;
        public ICollection<ProjectTask> ProjectTasks { get; set; } = new List<ProjectTask>();
    }
}
