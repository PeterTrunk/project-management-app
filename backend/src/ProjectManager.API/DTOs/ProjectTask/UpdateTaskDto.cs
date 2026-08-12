namespace ProjectManager.API.DTOs.ProjectTask
{
    public class UpdateTaskDto
    {
        /// <summary>
        /// Task címe, max 200 karakter
        /// </summary>
        public string? Title { get; set; }
        /// <summary>
        /// Opcionális Task leírás, max 250 karakter
        /// </summary>
        public string? Description { get; set; }
        public Guid? BoardId { get; set; }
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
        /// Opcionális Task határidő
        /// </summary>
        public DateTime? DueDate { get; set; }
        public uint RowVersion { get; set; }
    }
}
