namespace ProjectManager.API.DTOs.ProjectTask
{
    public class AssignTaskToBoardDto
    {
        public Guid? BoardId { get; set; }
        public uint RowVersion { get; set; }
    }
}