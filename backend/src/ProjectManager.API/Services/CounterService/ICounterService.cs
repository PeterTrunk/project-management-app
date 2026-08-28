namespace ProjectManager.API.Services.CounterService
{
    public interface ICounterService
    {
        Task<long> GetNextTaskNumberAsync(Guid projectId);
        Task<IReadOnlyList<long>> GetNextTaskNumbersAsync(Guid projectId, int count);
    }
}
