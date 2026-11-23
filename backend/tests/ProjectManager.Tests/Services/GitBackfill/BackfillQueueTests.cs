using ProjectManager.API.Services.GitBackfillService;

namespace ProjectManager.Tests.Services.GitBackfill
{
    /// <summary>
    /// Az automatikus pótlás várólistája.
    ///
    /// A lényeg nem a sorrend, hanem a <b>tele lista viselkedése</b>: a betétel ilyenkor
    /// hamisat ad, és nem dob el némán semmit. A hívó ebből tud naplózni, a nyilvántartási sor
    /// pedig `Pending` marad, tehát a felhasználó kézzel elindíthatja - egy némán eldobott
    /// munka éppen azt a hibát ismételné meg, amely ellen az egész funkció készült.
    /// </summary>
    public class BackfillQueueTests
    {
        [Fact]
        public async Task AnEnqueuedItem_ComesBackOut()
        {
            var queue = new BackfillQueue();
            var id = Guid.NewGuid();

            Assert.True(queue.TryEnqueue(id));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await foreach (var dequeued in queue.DequeueAllAsync(cts.Token))
            {
                Assert.Equal(id, dequeued);
                break;
            }
        }

        [Fact]
        public async Task ItemsComeOutInOrder()
        {
            var queue = new BackfillQueue();
            var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();

            foreach (var id in ids)
                Assert.True(queue.TryEnqueue(id));

            var dequeued = new List<Guid>();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            await foreach (var id in queue.DequeueAllAsync(cts.Token))
            {
                dequeued.Add(id);
                if (dequeued.Count == ids.Count) break;
            }

            Assert.Equal(ids, dequeued);
        }

        [Fact]
        public void AFullQueue_RefusesTheWriteInsteadOfDroppingIt()
        {
            var queue = new BackfillQueue();

            for (var i = 0; i < BackfillQueue.Capacity; i++)
                Assert.True(queue.TryEnqueue(Guid.NewGuid()));

            Assert.False(queue.TryEnqueue(Guid.NewGuid()));
        }

        [Fact]
        public async Task TheQueueIsNotBounded_ByTheReadersPace()
        {
            var queue = new BackfillQueue();

            //Olvasó nélkül is elfogad a kapacitásig: a webhook kérése nem várhat a
            //háttérfutásra, különben a betétel maga lenne a lassító
            for (var i = 0; i < BackfillQueue.Capacity; i++)
                Assert.True(queue.TryEnqueue(Guid.NewGuid()));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var count = 0;

            await foreach (var _ in queue.DequeueAllAsync(cts.Token))
            {
                if (++count == BackfillQueue.Capacity) break;
            }

            Assert.Equal(BackfillQueue.Capacity, count);

            //Kiolvasás után újra van hely
            Assert.True(queue.TryEnqueue(Guid.NewGuid()));
        }
    }
}
