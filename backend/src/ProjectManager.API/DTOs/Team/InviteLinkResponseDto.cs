namespace ProjectManager.API.DTOs.Team
{
    public class InviteLinkResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public int? MaxUses { get; set; }
        public int UseCount { get; set; }
        public string InviteUrl { get; set; } = string.Empty;
    }
}
