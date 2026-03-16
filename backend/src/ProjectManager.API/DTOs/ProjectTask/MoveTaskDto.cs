using System.ComponentModel.DataAnnotations;

namespace ProjectManager.API.DTOs.ProjectTask
{
    public class MoveTaskDto
    {
        /// <summary>
        /// Task cél-pozíciója egy oszlopban áthelyezés esetén
        /// </summary>
        public float Position { get; set; }
        /// <summary>
        /// Task cél-oszlopa áthelyezés esetén
        /// </summary>
        public Guid ColumnId { get; set; }
    }
}
