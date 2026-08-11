namespace ProjectManager.API.DTOs.ProjectTask
{
    public class AssignTaskToBoardDto
    {
        public Guid? BoardId { get; set; }
        public byte[] RowVersion { get; set; } = null!;
    }
}