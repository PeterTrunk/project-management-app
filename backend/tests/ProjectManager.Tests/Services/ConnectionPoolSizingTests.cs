using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ProjectManager.API.Data;
using ProjectManager.API.Extensions;

namespace ProjectManager.Tests.Services
{
    /// <summary>
    /// A kapcsolatkészlet méretezését rögzíti: a számítást, a tényleges alkalmazását, és azt
    /// az egyetlen összehasonlítást, amire az "üzemeltető felülírása nyer" szabály épül.
    ///
    /// Miért kell rá teszt: az `AddDatabase` a példányszámból származtatja a pool méretét,
    /// **kivéve** ha az üzemeltető explicit értéket adott a `DATABASE_URL`-ben. Ezt az
    /// alapértékhez való hasonlítás dönti el, mert az <see cref="NpgsqlConnectionStringBuilder"/>
    /// <c>ContainsKey</c>-e beállítás nélkül is igazat ad - lásd lentebb a zsákutca tesztjét.
    /// Ha ez a viselkedés egy csomagfrissítéssel megváltozik, a tesztek elbuknak, nem a
    /// terhelés alatt derül ki.
    /// </summary>
    public class ConnectionPoolSizingTests
    {
        private const string BaseConnectionString =
            "Host=localhost;Port=5432;Database=projectmanager;Username=pmuser;Password=pmpassword";

        [Theory]
        //Mérve: ez a két írásmód az, amit az Npgsql elfogad - a `Max Pool Size`,
        //a `maximumpoolsize` és a `MAX_POOL_SIZE` ArgumentException-t dob.
        [InlineData("Maximum Pool Size=50")]
        [InlineData("MaxPoolSize=50")]
        [InlineData("maximum pool size=50")]
        public void AnExplicitPoolSize_IsRecognisedRegardlessOfSpelling(string explicitSetting)
        {
            var builder = new NpgsqlConnectionStringBuilder($"{BaseConnectionString};{explicitSetting}");
            var defaultMaxPoolSize = new NpgsqlConnectionStringBuilder().MaxPoolSize;

            //Erre az egyetlen összehasonlításra épül az "üzemeltető felülírása nyer" szabály
            Assert.NotEqual(defaultMaxPoolSize, builder.MaxPoolSize);
            Assert.Equal(50, builder.MaxPoolSize);
        }

        [Fact]
        public void WithoutAnExplicitSetting_ThePoolSizeEqualsTheDefault()
        {
            //A másik irány: a tipikus connection stringnél a származtatásnak LE KELL futnia.
            //Ha ez nem állna, a pool korlátlan maradna, és visszakapnánk a hibát, amit a
            //származtatás megszüntet (két replika 200 kapcsolattal 100 ellen).
            var builder = new NpgsqlConnectionStringBuilder(BaseConnectionString);
            var defaultMaxPoolSize = new NpgsqlConnectionStringBuilder().MaxPoolSize;

            Assert.Equal(defaultMaxPoolSize, builder.MaxPoolSize);
        }

        [Fact]
        public void ContainsKey_IsUselessForDetectingAnExplicitSetting()
        {
            //Ez a teszt egy ZSÁKUTCÁT rögzít, nem egy követelményt. Az első megoldás a
            //`ContainsKey`-re épült, ami kézenfekvőnek tűnik - de az Npgsql erősen típusos
            //buildere minden ismert kulcsot alapértékkel tartalmaz, tehát BEÁLLÍTÁS NÉLKÜL IS
            //igazat ad. Erre épülve a származtatás soha nem futott volna le, és a hiba csak
            //terhelés alatt derült volna ki. Ha ez a viselkedés egyszer megváltozik, ez a
            //teszt bukik el, és akkor az egyszerűbb megoldás újra szóba jöhet.
            var builder = new NpgsqlConnectionStringBuilder(BaseConnectionString);

            Assert.True(builder.ContainsKey("Maximum Pool Size"));
        }

        [Theory]
        [InlineData("Max Pool Size=50")]
        [InlineData("maximumpoolsize=50")]
        [InlineData("MAX_POOL_SIZE=50")]
        public void AnInvalidKeyword_FailsLoudlyAtStartup(string invalidSetting)
        {
            //Fontos, hogy ez ne csendben kerüljön figyelmen kívül: egy elgépelt kulcs esetén
            //az üzemeltető azt hinné, beállította a poolt, pedig nem.
            Assert.Throws<ArgumentException>(() =>
                new NpgsqlConnectionStringBuilder($"{BaseConnectionString};{invalidSetting}"));
        }

        [Theory]
        //(100 - 20) / peldanyszam, 10-es alsó korláttal. A 9. példánynál lép életbe a padló.
        [InlineData(1, 80)]
        [InlineData(2, 40)]
        [InlineData(3, 26)]
        [InlineData(8, 10)]
        [InlineData(12, 10)]
        public void TheDerivedPoolSize_MatchesTheDocumentedFormula(int apiReplicas, int expected)
        {
            //Ugyanaz a számítás, mint az AddDatabase-ben. Szándékosan megismételve: ez az
            //OPERATIONS.md 3. fejezetének táblázatát rögzíti, amire az üzemeltető hagyatkozik.
            const int postgresMaxConnections = 100;
            const int reservedConnections = 20;
            const int minimumPoolSize = 10;

            var derived = (postgresMaxConnections - reservedConnections) / Math.Max(1, apiReplicas);

            Assert.Equal(expected, Math.Max(minimumPoolSize, derived));
        }

        [Theory]
        [InlineData(1, 80)]
        [InlineData(2, 40)]
        [InlineData(3, 26)]
        public void AddDatabase_AppliesTheDerivedPoolSize(int apiReplicas, int expectedPoolSize)
        {
            //Ez a lényegi állítás: a formula önmagában semmit nem bizonyít, ha az AddDatabase
            //nem is adja át az értéket a DbContextnek. A connection stringet ezért a
            //felépített DbContextOptionsból olvassuk vissza, nem a számításból.
            var services = new ServiceCollection();
            services.AddDatabase(BaseConnectionString, apiReplicas);

            using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<DbContextOptions<AppDbContext>>();
            //RelationalOptionsExtension.Extract, nem a provider belső NpgsqlOptionsExtensionje:
            //az utóbbi EF1001 figyelmeztetést ad (belső API), és a projekt 0 warningra épül.
            var applied = new NpgsqlConnectionStringBuilder(
                RelationalOptionsExtension.Extract(options).ConnectionString);

            Assert.Equal(expectedPoolSize, applied.MaxPoolSize);
        }

        [Fact]
        public void AddDatabase_KeepsAnExplicitPoolSize()
        {
            //Az üzemeltető felülírása nyer - ez az előző teszt párja.
            var services = new ServiceCollection();
            services.AddDatabase($"{BaseConnectionString};Maximum Pool Size=7", apiReplicas: 2);

            using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<DbContextOptions<AppDbContext>>();
            var applied = new NpgsqlConnectionStringBuilder(
                RelationalOptionsExtension.Extract(options).ConnectionString);

            Assert.Equal(7, applied.MaxPoolSize);
        }

        [Fact]
        public void UpToEightReplicas_TheTotalStaysWithinMaxConnections()
        {
            //A változtatás lényegi állítása: a példányok EGYÜTT nem kérhetnek többet, mint
            //amennyi van. Nyolc példányig ennek tartania kell; felette a padló miatt nem,
            //és ezért ad az induláskor WRN sort (DbPoolFloorReached).
            const int postgresMaxConnections = 100;
            const int reservedConnections = 20;

            for (var replicas = 1; replicas <= 8; replicas++)
            {
                var poolPerReplica = (postgresMaxConnections - reservedConnections) / replicas;
                var total = (poolPerReplica * replicas) + reservedConnections;

                Assert.True(
                    total <= postgresMaxConnections,
                    $"{replicas} példánynál a lefoglalható kapcsolatok száma {total}, "
                    + $"ami meghaladja a max_connections {postgresMaxConnections} értékét.");
            }
        }
    }
}
