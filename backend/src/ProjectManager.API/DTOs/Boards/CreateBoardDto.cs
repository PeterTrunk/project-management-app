namespace ProjectManager.API.DTOs.Boards
{
    public class CreateBoardDto
    {
        public Guid ProjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsDefault { get; set; } 
    }
}
