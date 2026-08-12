namespace ProjectManager.API.DTOs.Columns
{
    public class ColumnResponseDto
    {
        public Guid Id { get; set; }
        public Guid BoardId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string MapsToStatus { get; set; } = string.Empty;
        public int? WipLimit { get; set; }
        public int Position { get; set; }
        public uint RowVersion { get; set; }
    }
}
