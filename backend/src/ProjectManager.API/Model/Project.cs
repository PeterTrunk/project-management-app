namespace ProjectManager.API.Model
{
    public class Project
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ProjKey { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid OwnerId { get; set; }
        public bool IsArchived { get; set; } = false;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public User Owner { get; set; } = null!;
        public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
        public ProjectCounter ProjectCounter { get; set; } = null!;
        public ICollection<Integration> Integrations { get; set; } = new List<Integration>();
        public ICollection<Board> Boards { get; set; } = new List<Board>();
        public ICollection<Sprint> Sprints { get; set;} = new List<Sprint>();
        public ICollection<ProjectTask> ProjectTasks { get; set; } = new List<ProjectTask>();
        public ICollection<Label> Labels { get; set; } = new List<Label>();
        public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
        public ICollection<ProjectInvite> Invites { get; set; } = new List<ProjectInvite>();
    }
}