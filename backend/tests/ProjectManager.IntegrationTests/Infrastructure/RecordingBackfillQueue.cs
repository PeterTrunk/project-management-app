using ProjectManager.API.Services.GitBackfillService;

namespace ProjectManager.IntegrationTests.Infrastructure
{
    /// <summary>
    /// Az automatikus pótlás várólistájának dupla.
    ///
    /// A valódi várólistát egy háttérfutás olvassa, tehát éles példánnyal a teszt a saját
    /// szálán nem látná, mi került be - itt pedig pont ez a kérdés: a webhook betette-e a
    /// sort, és csak akkor, ha az integráción be van kapcsolva.
    ///
    /// A tele lista állapota kézzel beállítható: az nem hibaág, hanem döntés - a sor ilyenkor
    /// Pending marad, és a felhasználó indíthatja kézzel.
    /// </summary>
    public sealed class RecordingBackfillQueue : IBackfillQueue
    {
        private readonly List<Guid> _enqueued = new();

        /// <summary>A várólistára tett azonosítók, sorrendben.</summary>
        public IReadOnlyList<Guid> Enqueued => _enqueued;

        /// <summary>Igazra állítva a betétel meghiúsul, mint teli listánál.</summary>
        public bool IsFull { get; set; }

        public bool TryEnqueue(Guid backfillId)
        {
            if (IsFull) return false;

            _enqueued.Add(backfillId);
            return true;
        }

        public async IAsyncEnumerable<Guid> DequeueAllAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            //A tesztek a feldolgozást közvetlenül hívják (RunAutomaticAsync), nem a
            //háttérfutáson keresztül: itt nincs mit kiolvasni.
            await Task.CompletedTask;
            yield break;
        }
    }
}
