using System.ComponentModel.DataAnnotations;

namespace ProjectManager.API.DTOs.ProjectTask
{
    public class MoveTaskDto
    {
        /// <summary>
        /// Mozgatandó Task Utáni Task Id-ja (ha van) egy oszlopban áthelyezés esetén
        /// </summary>
        public Guid? AfterTaskId { get; set; }
        /// <summary>
        /// Task cél-oszlopa áthelyezés esetén
        /// </summary>
        public Guid? ColumnId { get; set; }
    }
}
