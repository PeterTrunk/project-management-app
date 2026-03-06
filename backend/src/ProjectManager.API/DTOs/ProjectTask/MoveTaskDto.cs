using System.ComponentModel.DataAnnotations;

namespace ProjectManager.API.DTOs.Task
{
    public class MoveTaskDto
    {
        [Required]
        public float Position { get; set; }
        [Required]
        public Guid ColumnId { get; set; }
    }
}
