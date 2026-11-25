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

        /// <summary>
        /// Átfutási idő: az első hozzá kapcsolt committól a pull request összeolvadásáig, azokra
        /// a feladatokra, amelyeknek az összeolvadása az időszakba esik.
        /// </summary>
        Task<List<CycleTimeDataPointDto>> GetCycleTimeAsync(Guid projectId, DateTime dateFrom, DateTime dateTo);
    }
}
