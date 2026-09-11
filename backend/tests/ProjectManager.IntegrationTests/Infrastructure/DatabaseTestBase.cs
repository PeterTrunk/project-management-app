using ProjectManager.API.Data;

namespace ProjectManager.IntegrationTests.Infrastructure
{
    /// <summary>
    /// Közös ős az adatbázist használó tesztosztályoknak: gondoskodik a tiszta kezdőállapotról.
    /// </summary>
    [Collection(PostgresCollection.Name)]
    public abstract class DatabaseTestBase : IAsyncLifetime
    {
        protected PostgresFixture Fixture { get; }

        protected DatabaseTestBase(PostgresFixture fixture) => Fixture = fixture;

        /// <summary>Az xUnit ezt minden teszt előtt lefuttatja.</summary>
        public Task InitializeAsync() => Fixture.ResetAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        /// <summary>Új, önálló context - lásd a fixture magyarázatát a change trackerről.</summary>
        protected AppDbContext CreateContext() => Fixture.CreateContext();
    }
}
