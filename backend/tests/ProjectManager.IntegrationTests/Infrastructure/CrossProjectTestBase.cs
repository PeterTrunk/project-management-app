namespace ProjectManager.IntegrationTests.Infrastructure
{
    /// <summary>
    /// Közös alap a projekt-scoping (IDOR) tesztekhez.
    ///
    /// A minta minden esetben ugyanaz: az EGYIK projekt azonosítójával nyúlunk a MÁSIK projekt entitásához.
    /// A helyes válasz NotFoundException - nem ForbiddenException,
    /// mert a hívónak azt sem kell megtudnia, hogy az entitás egyáltalán létezik.
    ///
    /// A mutáló metódusoknál a kivétel önmagában nem elég: friss contexttel ellenőrizzük,
    /// hogy a sor tényleg megvan még. Egy szolgáltatás dobhatna kivételt AZUTÁN is, hogy már törölt vagy módosított valamit.
    /// </summary>
    public abstract class CrossProjectTestBase : DatabaseTestBase
    {
        protected CrossProjectTestBase(PostgresFixture fixture) : base(fixture) { }

        /// <summary>
        /// Két független projekt, külön tulajdonossal.
        ///
        /// A seedelés külön contexttel fut, hogy a vizsgált szolgáltatás FRISS change
        /// trackerrel induljon - pont mint élesben, ahol minden kérés saját DbContextet kap.
        /// Közös context mellett a már betöltött entitások elfedhetnék a hiányzó szűrést.
        /// </summary>
        protected async Task<TwoProjects> SeedTwoProjectsAsync()
        {
            await using var seedContext = CreateContext();
            return await TestData.SeedTwoProjectsAsync(seedContext);
        }

        /// <summary>Friss contexttel ellenőrzi, hogy a sor még létezik.</summary>
        protected async Task AssertStillExistsAsync<TEntity>(Guid id) where TEntity : class
        {
            await using var verify = CreateContext();
            var entity = await verify.FindAsync<TEntity>(id);

            Assert.True(entity != null,
                $"A(z) {typeof(TEntity).Name} sor eltűnt, pedig a hívás idegen projektből érkezett.");
        }
    }
}
