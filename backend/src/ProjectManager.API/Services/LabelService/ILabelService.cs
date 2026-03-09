using ProjectManager.API.DTOs.Labels;

namespace ProjectManager.API.Services.LabelService
{
    public interface ILabelService
    {
        Task<List<LabelResponseDto>> GetLabelsAsync(Guid projectId);
        Task<LabelResponseDto> CreateLabelAsync(Guid projectId, CreateLabelDto dto);
        Task DeleteLabelAsync(Guid projectId, Guid labelId);
        Task AddLabelToTaskAsync(Guid projectId, Guid taskId, Guid labelId);
        Task RemoveLabelFromTaskAsync(Guid projectId, Guid taskId, Guid labelId);
    }
}
