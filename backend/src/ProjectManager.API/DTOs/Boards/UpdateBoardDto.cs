namespace ProjectManager.API.DTOs.Boards
{
    public class UpdateBoardDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool? IsDefault { get; set; }
    }
}
