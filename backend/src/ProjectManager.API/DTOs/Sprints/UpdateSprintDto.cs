namespace ProjectManager.API.DTOs.Sprints
{
    public class UpdateSprintDto
    {
        public Guid? BoardId { get; set; }
        /// <summary>
        /// Sprint neve, maximum 80 karakter
        /// </summary>
        public string? Name { get; set; }
        /// <summary>
        /// Sprint cél leírása, maximum 500 karakter
        /// </summary>
        public string? Goal { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public byte[] RowVersion { get; set; } = null!;
    }
}
