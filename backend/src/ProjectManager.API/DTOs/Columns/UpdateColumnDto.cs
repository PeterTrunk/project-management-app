namespace ProjectManager.API.DTOs.Columns
{
    public class UpdateColumnDto
    {
        public string? Name { get; set; }
        public string? MapsToStatus { get; set; }
        public int? WipLimit { get; set; }
        public int? Position { get; set; }
    }
}
