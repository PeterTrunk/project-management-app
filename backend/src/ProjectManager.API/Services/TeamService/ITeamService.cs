using ProjectManager.API.DTOs.Team;

namespace ProjectManager.API.Services.TeamService
{
    public interface ITeamService
    {
        Task<List<ProjectMemberResponseDto>> GetMembersAsync(Guid projectId);
        Task RemoveMemberAsync(Guid projectId, Guid userId);
        Task<ProjectMemberResponseDto> UpdateMemberRoleAsync(Guid projectId, Guid userId, UpdateMemberRoleDto dto);
        Task<InviteLinkResponseDto> GenerateInviteLinkAsync(Guid projectId, GenerateInviteLinkDto dto);
        Task<ProjectMemberResponseDto> JoinProjectAsync(string token);
    }
}
