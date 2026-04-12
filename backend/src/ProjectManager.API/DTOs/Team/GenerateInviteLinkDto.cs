namespace ProjectManager.API.DTOs.Team
{
    public class GenerateInviteLinkDto
    {
        public int? MaxUses { get; set; }
        public int? ExpiresInDays { get; set; }
    }
}
