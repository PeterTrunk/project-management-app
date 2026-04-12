namespace ProjectManager.API.Model
{
    public class ProjectInvite
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid CreatedById { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public int? MaxUses { get; set; } // null = korlátlan
        public int UseCount { get; set; } = 0;
        public DateTime CreatedAt { get; set; }
        
        public Project Project { get; set; } = null!;
        public User CreatedBy { get; set; } = null!;
    }
}
