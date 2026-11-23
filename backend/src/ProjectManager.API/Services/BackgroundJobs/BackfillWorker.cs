using ProjectManager.API.Services.GitBackfillService;

namespace ProjectManager.API.Services.BackgroundJobs
{
    /// <summary>
    /// Az automatikus commit-pótlás háttérfutása.
    ///
    /// A webhook csak beteszi a nyilvántartási sor azonosítóját a várólistára, és válaszol -
    /// a szolgáltató így sosem lát lassú webhookot, akkor sem, ha a compare API lapoz. A
    /// tényleges lekérés innen indul, a kérésen kívül, saját hatókörrel.
    ///
    /// A többi háttérfutással szemben ez NEM időzített: nincs mit körbejárni, az esemény maga
    /// a hiány észlelése.
    /// </summary>
    public class BackfillWorker : BackgroundService
    {
        private readonly IBackfillQueue _queue;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BackfillWorker> _logger;

        public BackfillWorker(
            IBackfillQueue queue,
            IServiceProvider serviceProvider,
            ILogger<BackfillWorker> logger)
        {
            _queue = queue;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await foreach (var backfillId in _queue.DequeueAllAsync(stoppingToken))
                {
                    await RunSafelyAsync(backfillId);
                }
            }
            catch (OperationCanceledException)
            {
                //Leállás: nem hiba. A félbehagyott futás foglalása elévül, tehát a sor
                //újraindítható marad - pont ezért van a foglalásnak lejárata.
            }
        }

        /// <summary>
        /// A BackgroundServiceExceptionBehavior alapértelmezése StopHost: egy kezeletlen
        /// kivétel innen a TELJES API-példányt leállítaná. Egy elérhetetlen git szolgáltató
        /// nem állíthatja le a szolgáltatást - a lekérő amúgy is besorolt eredményt ad, ez a
        /// védelem a váratlan hibákra van.
        /// </summary>
        private async Task RunSafelyAsync(Guid backfillId)
        {
            try
            {
                //Saját hatókör: a webhook kérésének hatóköre a válasszal együtt megszűnt,
                //tehát a kontextusa sem használható
                using var scope = _serviceProvider.CreateScope();
                var backfillService = scope.ServiceProvider.GetRequiredService<IGitBackfillService>();

                await backfillService.RunAutomaticAsync(backfillId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Automatikus commit pótlás váratlan hiba | BackfillId: {BackfillId}", backfillId);
            }
        }
    }
}
