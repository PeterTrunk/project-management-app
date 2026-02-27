namespace ProjectManager.API.Model
{
    public class ProjectMember
    {
        public Guid ProjectId { get; set; }
        public Guid UserId { get; set; }
        public string ProjectRole { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; }

        public User User { get; set; } = null!;
        public Project Project { get; set; } = null!;
    }
}
