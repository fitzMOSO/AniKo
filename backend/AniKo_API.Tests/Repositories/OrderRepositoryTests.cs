using AniKo_API.Data.Seed;
using AniKo_API.Models;
using AniKo_API.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AniKo_API.Tests.Repositories;

/// <summary>
/// <see cref="OrderRepository"/> against the seeded demo dataset in a real Postgres.
/// <para>
/// Expected values are recomputed from <see cref="DemoDataSeeder"/>'s builders rather than
/// retyped as literals. The builders are pure and deterministic, so this is not a tautology —
/// it asserts that what the repository reads back is what the seeder wrote, and it means an
/// edit to the seed changes these tests' expectations instead of breaking them for no reason.
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class OrderRepositoryTests
{
    private readonly PostgresFixture _fixture;

    public OrderRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

    /// <summary>The eight seeded orders, in the order the seeder built them (newest first).</summary>
    private static List<Order> SeededOrders()
    {
        var users = DemoDataSeeder.BuildUsers();
        var suppliers = DemoDataSeeder.BuildSuppliers(users);
        var listings = DemoDataSeeder.BuildListings(suppliers);

        return DemoDataSeeder.BuildOrders(users, listings);
    }

    // ── ListAsync / FindAsync ────────────────────────────────────────────────

    [Fact]
    public async Task ListAsyncReturnsEverySeededOrder()
    {
        await using var db = _fixture.CreateContext();
        var repository = new OrderRepository(db);

        var orders = await repository.ListAsync();

        Assert.Equal(8, orders.Count);
    }

    /// <summary>
    /// The generic base's key lookup, exercised on a real provider. It is the one method that
    /// builds its predicate with <c>EF.Property</c> rather than from a lambda over a property, so
    /// it is the one method whose translation is not obviously correct by inspection.
    /// </summary>
    [Fact]
    public async Task FindAsyncReturnsTheRowWithThatId()
    {
        await using var db = _fixture.CreateContext();
        var repository = new OrderRepository(db);

        var expected = await db.Orders.AsNoTracking().SingleAsync(o => o.Reference == "AK-1001");
        var found = await repository.FindAsync(expected.Id);

        Assert.NotNull(found);
        Assert.Equal("AK-1001", found.Reference);
    }

    [Fact]
    public async Task FindAsyncReturnsNullForAnUnknownId()
    {
        await using var db = _fixture.CreateContext();
        var repository = new OrderRepository(db);

        Assert.Null(await repository.FindAsync(999_999));
    }

    /// <summary>
    /// Reads are untracked, and this is the assertion that would fail if the base class ever lost
    /// its <c>AsNoTracking</c> — nothing else in the suite would notice, because a tracked read
    /// returns the same rows.
    /// </summary>
    [Fact]
    public async Task ReadsDoNotEnterTheChangeTracker()
    {
        await using var db = _fixture.CreateContext();
        var repository = new OrderRepository(db);

        await repository.ListAsync();

        Assert.Empty(db.ChangeTracker.Entries());
    }

    // ── ListRecentAsync ──────────────────────────────────────────────────────

    /// <summary>
    /// Ordering is the contract, not an incidental property of the seed: "recent" means newest
    /// first, and a query that returned the right eight rows in insertion order would render a
    /// table that is simply wrong.
    /// </summary>
    [Fact]
    public async Task ListRecentAsyncOrdersByCreatedAtDescending()
    {
        await using var db = _fixture.CreateContext();
        var repository = new OrderRepository(db);

        var rows = await repository.ListRecentAsync(20);

        Assert.Equal(8, rows.Count);
        Assert.Equal(
            rows.Select(r => r.CreatedAt).OrderByDescending(t => t).ToList(),
            rows.Select(r => r.CreatedAt).ToList());
    }

    [Fact]
    public async Task ListRecentAsyncRespectsTheLimitAndTakesTheNewest()
    {
        await using var db = _fixture.CreateContext();
        var repository = new OrderRepository(db);

        var rows = await repository.ListRecentAsync(3);

        var expected = SeededOrders()
            .OrderByDescending(o => o.CreatedAt)
            .Take(3)
            .Select(o => o.Reference)
            .ToList();

        Assert.Equal(3, rows.Count);
        Assert.Equal(expected, rows.Select(r => r.Reference).ToList());
    }

    /// <summary>
    /// The names come from two joins deep — order → listing → supplier — and neither is a column
    /// on the order. A projection that silently dropped one would surface as an empty cell in the
    /// table rather than as an error.
    /// </summary>
    [Fact]
    public async Task ListRecentAsyncResolvesListingAndSupplierNames()
    {
        await using var db = _fixture.CreateContext();
        var repository = new OrderRepository(db);

        var newest = (await repository.ListRecentAsync(1)).Single();

        Assert.Equal("AK-1001", newest.Reference);
        Assert.Equal("Premium White Rice", newest.ListingName);
        Assert.Equal("Bataan Rice Growers", newest.SupplierName);
        Assert.Equal(1_500, newest.QuantityKg);
        Assert.Equal(OrderStatus.Confirmed, newest.Status);
    }

    // ── ListSinceAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task ListSinceAsyncReturnsEveryOrderWhenTheWindowPredatesTheSeed()
    {
        await using var db = _fixture.CreateContext();
        var repository = new OrderRepository(db);

        var rows = await repository.ListSinceAsync(PostgresFixture.SeedEpoch.AddYears(-5));

        Assert.Equal(8, rows.Count);
    }

    /// <summary>
    /// The boundary is inclusive — <c>&gt;=</c>, not <c>&gt;</c>. Off by one here means an order
    /// placed exactly on the window's first instant is counted in neither the current period nor
    /// the comparison period, and the dashboard's totals stop summing to the order count.
    /// </summary>
    [Fact]
    public async Task ListSinceAsyncIncludesAnOrderCreatedExactlyAtTheBoundary()
    {
        await using var db = _fixture.CreateContext();
        var repository = new OrderRepository(db);

        var seeded = SeededOrders().OrderByDescending(o => o.CreatedAt).ToList();
        var boundary = seeded[3].CreatedAt;

        var rows = await repository.ListSinceAsync(boundary);

        Assert.Equal(4, rows.Count);
        Assert.Contains(rows, r => r.CreatedAt == boundary);
    }

    /// <summary>
    /// Supplier and price are read through the listing. Both are what the stats are computed
    /// from, and neither exists on the orders table.
    /// </summary>
    [Fact]
    public async Task ListSinceAsyncFlattensSupplierAndPriceFromTheListing()
    {
        await using var db = _fixture.CreateContext();
        var repository = new OrderRepository(db);

        var expectedSupplierId = await db.Suppliers
            .AsNoTracking()
            .Where(s => s.Name == "Bataan Rice Growers")
            .Select(s => s.Id)
            .SingleAsync();

        var rows = await repository.ListSinceAsync(PostgresFixture.SeedEpoch.AddYears(-5));
        var newest = rows.MaxBy(r => r.CreatedAt);

        Assert.NotNull(newest);
        Assert.Equal(expectedSupplierId, newest.SupplierId);

        // "Premium White Rice", the lot AK-1001 was placed against.
        Assert.Equal(58.50m, newest.PricePerKg);
        Assert.Equal(1_500, newest.QuantityKg);
    }
}
