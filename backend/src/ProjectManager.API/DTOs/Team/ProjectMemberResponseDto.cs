namespace ProjectManager.API.DTOs.Team
{
    public class ProjectMemberResponseDto
    {
        public Guid UserId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string ProjectRole { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; }
    }
}
