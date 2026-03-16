using System.ComponentModel.DataAnnotations;

namespace ProjectManager.API.DTOs.ProjectTask
{
    public class CreateTaskDto
    {
        /// <summary>
        /// Task címe, max 200 karakter
        /// </summary>
        public string Title { get; set; } = string.Empty;
        /// <summary>
        /// Opcionális Task leírás, max 250 karakter
        /// </summary>
        public string? Description { get; set; }
        public Guid BoardId { get; set; }
        public Guid ColumnId { get; set; }
        public Guid? SprintId { get; set; }
        /// <summary>
        /// Task prioritás, prioritások: low, medium, high, critical
        /// </summary>
        public string? Priority { get; set; }
        /// <summary>
        /// Opcionális Task becsült elvégzési ideje percekben
        /// </summary>
        public int? EstimateInMinutes { get; set; }
        /// <summary>
        /// Opcionális Task határidő, nem lehet multbeli
        /// </summary>
        public DateTime? DueDate { get; set; }
    }
}
