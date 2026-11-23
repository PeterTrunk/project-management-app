using System.Threading.Channels;

namespace ProjectManager.API.Services.GitBackfillService
{
    /// <summary>
    /// Az automatikusan megkísérelt pótlások várólistája.
    ///
    /// Azért kell egyáltalán várólista, mert az automatikus kísérlet NEM futhat a webhook
    /// kérésében: a szolgáltató API-jának megkérdezése a webhook válaszidejét a szolgáltató
    /// válaszidejéhez kötné, a lassan válaszoló webhookot pedig a GitHub és a GitLab is
    /// hibásnak jelöli, és végül kikapcsolja. A webhook tehát leírja a sort, válaszol, és
    /// csak beteszi az azonosítót ide - a lekérés egy háttérfutásban indul.
    ///
    /// A várólista folyamaton belüli, és ez tudatos: azt a pushot, amelynek a hiányát egy
    /// példány észlelte, ugyanaz a példány pótolja. Több API-példánynál sincs szükség
    /// megosztott sorra, mert a sort az adatbázisbeli foglalás védi, nem a várólista.
    /// </summary>
    public interface IBackfillQueue
    {
        /// <summary>
        /// Hamis, ha a várólista tele van. Ilyenkor NEM dobunk el némán semmit: a
        /// nyilvántartási sor `Pending` marad, tehát a felhasználó kézzel elindíthatja.
        /// </summary>
        bool TryEnqueue(Guid backfillId);

        IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken);
    }

    public sealed class BackfillQueue : IBackfillQueue
    {
        /// <summary>
        /// Felső korlát. Enélkül egy hibás szolgáltatói állapot (sok csonkolt push rövid idő
        /// alatt) korlátlanul növelné a memóriában tartott munkát - és a pótlás a szolgáltatói
        /// kvótát is égeti, tehát a torlódás nem megoldás, hanem tünet.
        /// </summary>
        public const int Capacity = 100;

        //A Wait teljes listánál hamisat ad a TryWrite-ra, blokkolás helyett: a DropWrite
        //éppen azt tenné, amit a funkció gyógyítani akar - némán eldobná a munkát.
        private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(
            new BoundedChannelOptions(Capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });

        public bool TryEnqueue(Guid backfillId) => _channel.Writer.TryWrite(backfillId);

        public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken) =>
            _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
