using ProjectManager.API.DTOs.Activity;

namespace ProjectManager.API.Services.ActivityService
{
    public interface IActivityService
    {
        Task<ActivityResponseDto> LogActivityAsync(Guid projectId, string entityType, Guid entityId, string action, string description, string? payload = null);
        Task<List<ActivityResponseDto>> GetActivitiesAsync(Guid projectId, int page = 1, int pageSize = 20);
        Task<ActivityResponseDto> LogSystemActivityAsync(Guid projectId, string entityType, Guid entityId, string action, string description, string? payload = null);
    }
}
