using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProjectManager.API.Data;
using Respawn;
using Testcontainers.PostgreSql;

namespace ProjectManager.IntegrationTests.Infrastructure
{
    /// <summary>
    /// Egy valódi PostgreSQL adatbázist ad a teszteknek, Docker konténerben.
    ///
    /// Miért nem EF Core InMemory: az AppDbContext PostgreSQL-specifikus elemekre épül, 
    /// amiket az InMemory vagy nem tud, vagy némán elfogad. 
    /// Az xmin konkurenciavezérlés xid rendszeroszlopra képződik:
    /// - AuthService ExecuteUpdateAsync-et hív, 
    /// - CounterService Serializable tranzakciót nyit és PostgresException 40001-re épít, 
    /// - A séma ~12  indexet tartalmaz, amiket az InMemory nem tartat be. Egy hamis zöld teszt rosszabb, mint a teszt hiánya.
    /// 
    /// Élettartam: a konténer FUTÁSONKÉNT EGYSZER indul (ICollectionFixture), 
    /// nem tesztenként mert az másodperceket jelentene tesztenként. 
    /// A tesztek elválasztását a Respawn adja.
    /// </summary>
    public class PostgresFixture : IAsyncLifetime
    {
        /// <summary>
        /// Kimenekülő út Docker nélkül: ha be van állítva, ezt a meglévő adatbázist használjuk.
        ///
        /// FIGYELEM: ide SOHA ne a docker-compose.yml fejlesztői adatbázisa kerüljön.
        /// A Respawn minden táblát ürít, tehát a saját fejlesztői adataidat vinné el.
        /// </summary>
        private const string ExternalConnectionEnvVar = "PMA_TEST_POSTGRES";

        //Ugyanaz a major verzió, mint a docker-compose.yml-ben és élesben
        private const string PostgresImage = "postgres:17";

        private readonly PostgreSqlContainer? _container;
        private Respawner _respawner = null!;

        public string ConnectionString { get; private set; } = string.Empty;

        public PostgresFixture()
        {
            var external = Environment.GetEnvironmentVariable(ExternalConnectionEnvVar);
            if (!string.IsNullOrWhiteSpace(external))
            {
                ConnectionString = external;
                return;
            }

            //A konténer belső 5432-esét a Testcontainers egy szabad hosztportra képezi le,
            //ezért nem ütközik a fejlesztői PostgreSQL-lel, ami az 5432-őn ül.
            _container = new PostgreSqlBuilder(PostgresImage).Build();
        }

        public async Task InitializeAsync()
        {
            if (_container != null)
            {
                //A StartAsync megvárja, amíg az adatbázis ténylegesen fogad kapcsolatot -
                //nem elég, hogy a konténer elindult, különben az első teszt véletlenszerűen elhasalna.
                await _container.StartAsync();
                ConnectionString = _container.GetConnectionString();
            }

            //Szándékosan Migrate és nem EnsureCreated: a szűrt egyedi index és az xmin leképzés
            //pont az, amit valódi DDL ellen kell futtatni. Futásonként egyszer.
            await using (var context = CreateContext())
            {
                await context.Database.MigrateAsync();
            }

            //A Respawn 7 nyitott kapcsolatot vár, nem connection stringet
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();

            _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                //A séma nem tartalmaz seed adatot, tehát minden tábla üríthető.
                //A migrációs előzményt viszont meg kell tartani, különben újra lefutna a migráció.
                TablesToIgnore = [new Respawn.Graph.Table("__EFMigrationsHistory")]
            });
        }

        /// <summary>
        /// Minden teszt ELŐTT fut, nem utána. 
        /// Így egy elszállt teszt állapota megvizsgálható marad, a következő teszt mégis tisztán indul.
        /// </summary>
        public async Task ResetAsync()
        {
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            await _respawner.ResetAsync(connection);
        }

        /// <summary>
        /// Új, önálló contextet ad. A teszteknek gyakran kell egy MÁSODIK context: 
        /// A change tracker különben olyan sorokat is "meglévőnek" mutatna, amiket csak a memória őriz.
        /// </summary>
        public AppDbContext CreateContext() =>
            new(new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(ConnectionString)
                .Options);

        public async Task DisposeAsync()
        {
            if (_container != null)
                await _container.DisposeAsync();
        }
    }
}
