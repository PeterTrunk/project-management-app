using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProjectManager.API.Data;
using System.Data;

namespace ProjectManager.API.Services.CounterService
{
    public class CounterService : ICounterService
    {
        private readonly AppDbContext _context;

        public CounterService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<long> GetNextTaskNumberAsync(Guid projectId)
        {
            var numbers = await GetNextTaskNumbersAsync(projectId, 1);
            return numbers[0];
        }

        public async Task<IReadOnlyList<long>> GetNextTaskNumbersAsync(Guid projectId, int count)
        {
            var maxRetries = 3;
            
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                    try
                    {
                        var counter = await _context.ProjectCounters
                            .FirstOrDefaultAsync(pc => pc.ProjectId == projectId);

                        if (counter == null)
                            throw new Exception("Számláló nem található");

                        var startNum = counter.LastNum + 1;
                        counter.LastNum += count;

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        return Enumerable.Range(0, count)
                                         .Select(i => startNum + i)
                                         .ToList();
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
                catch (PostgresException ex) when (ex.SqlState == "40001") //serialization failure a PG-ben
                {
                    _context.ChangeTracker.Clear();

                    if (attempt == maxRetries - 1)
                        throw new Exception("A task létrehozása nem sikerült, kérjük próbáld újra!");

                    await Task.Delay(TimeSpan.FromMilliseconds(50 * (attempt + 1)));
                }
            }

            throw new Exception("A task létrehozása nem sikerült, kérjük próbáld újra!");
        }
    }
}
