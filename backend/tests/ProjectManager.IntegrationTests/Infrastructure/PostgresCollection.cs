namespace ProjectManager.IntegrationTests.Infrastructure
{
    /// <summary>
    /// Minden adatbázisos tesztosztály ebbe a collectionbe tartozik, 
    /// így a konténer futásonként egyszer indul. 
    /// A `xunit.runner.json` kikapcsolja a collection-szintű párhuzamosítást:
    /// egyetlen adatbázison osztozunk, és a Respawn reset belefutna egy párhuzamosan futó teszt közepébe.
    /// </summary>
    [CollectionDefinition(Name)]
    public class PostgresCollection : ICollectionFixture<PostgresFixture>
    {
        public const string Name = "Postgres";
    }
}
