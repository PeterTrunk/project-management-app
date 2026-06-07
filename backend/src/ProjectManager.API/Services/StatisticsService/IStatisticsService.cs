using ProjectManager.API.DTOs.Statistics;

namespace ProjectManager.API.Services.StatisticsService
{
    public interface IStatisticsService
    {
        Task<List<TaskStatusDistributionDto>> GetTaskStatusDistributionAsync(Guid projectId, Guid? sprintId = null);
        Task<List<BurndownDataPointDto>> GetBurndownAsync(Guid projectId, Guid sprintId);
        Task<List<WorkloadDataPointDto>> GetWorkloadAsync(Guid projectId, Guid? sprintId = null);
        Task<List<VelocityDataPointDto>> GetVelocityAsync(Guid projectId);
        Task<List<CumulativeFlowDataPointDto>> GetCumulativeFlowAsync(Guid projectId, DateTime dateFrom, DateTime dateTo, Guid? boardId = null);
    }
}
