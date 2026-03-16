using System.ComponentModel.DataAnnotations;

namespace ProjectManager.API.DTOs.ProjectTask
{
    public class MoveTaskDto
    {
        public float Position { get; set; }
        public Guid ColumnId { get; set; }
    }
}
