using AniKo_API.Data;
using AniKo_API.Data.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace AniKo_API.Tests.Repositories;

/// <summary>
/// One Postgres container, migrated and seeded once, shared by every repository test.
/// <para>
/// The repositories are almost entirely LINQ that has to survive translation to SQL, and that is
/// not something an in-memory provider can tell you about: <c>GroupBy</c> with an <c>Average</c>,
/// a <c>Distinct</c> over a projected pair, <c>numeric(18,2)</c> arithmetic and
/// <c>timestamp with time zone</c> comparisons all behave differently — or simply work — in a
/// provider that never generates SQL. A test suite that passes against the in-memory provider and
/// throws <c>InvalidOperationException</c> on the first real request is worse than no test.
/// </para>
/// <para>
/// A <i>collection</i> fixture rather than a class fixture because the container costs seconds to
/// start and the migration costs more, and xUnit creates a class fixture per test class. Five
/// classes would be five containers, five migrations and five seeds to read data none of them
/// modify.
/// </para>
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // Pinned, not `latest`. An unpinned tag makes the day Postgres changes a default the day this
    // suite fails for a reason nowhere in the diff. Passed to the constructor rather than through
    // WithImage because the parameterless overload is obsolete as of Testcontainers 4.14.
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("aniko_test")
        .Build();

    /// <summary>
    /// The seeded epoch, restated as the tests' notion of "now". The seeder never reads the
    /// clock, so every assertion about recency has to be anchored to this rather than to
    /// <c>DateTime.UtcNow</c> — otherwise the tests start failing on their own, some months from
    /// now, with no code change.
    /// </summary>
    public static DateTime SeedEpoch => DemoDataSeeder.SeedEpoch;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var db = CreateContext();

        // Migrate, not EnsureCreated. EnsureCreated builds the schema from the model and skips
        // the migrations entirely, which would mean the crops reference data — it arrives through
        // the InitialCreate migration's HasData, not through DemoDataSeeder — silently is not
        // there, and every join to crops would return nothing.
        await db.Database.MigrateAsync();

        await DemoDataSeeder.SeedAsync(db, NullLogger.Instance);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>
    /// A fresh context per call. Tests must not share one: a context carries a change tracker and
    /// a first-level cache, and two assertions reading the same row through one context can be
    /// answered from memory rather than from Postgres — which is the thing under test.
    /// </summary>
    public AniKoDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AniKoDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        return new AniKoDbContext(options);
    }
}

/// <summary>
/// The one collection that needs Docker.
/// <para>
/// Everything else in this project — the model tests, the seeder tests, the <c>ApiFactory</c>
/// smoke tests — deliberately runs without a database, and that property is worth more than it
/// looks: it is what lets a contributor run <c>dotnet test</c> on a laptop with no daemon and
/// still learn something. Naming the collection here, and applying it only to the classes in this
/// folder, is what keeps the container's blast radius to those classes.
/// </para>
/// <para>
/// It also serialises them, which these tests rely on: they share one seeded database, and the
/// two that must insert a row (a supplier with no listings, a second region for one month) delete
/// it again in a <c>finally</c>. That clean-up is only sufficient because xUnit does not run two
/// classes in the same collection concurrently.
/// </para>
/// </summary>
[CollectionDefinition(PostgresCollection.Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
