using AniKo_API.Models;
using AniKo_API.Repositories;

namespace AniKo_API.Tests.Repositories;

/// <summary>
/// <see cref="SupplierRepository"/>, and in particular the derived crop list — the one query in
/// this layer that has no column behind its result.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SupplierRepositoryTests
{
    private readonly PostgresFixture _fixture;

    public SupplierRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ListAsyncReturnsEverySeededSupplierIncludingUnverified()
    {
        await using var db = _fixture.CreateContext();
        var repository = new SupplierRepository(db);

        Assert.Equal(6, (await repository.ListAsync()).Count);
    }

    [Fact]
    public async Task ListVerifiedWithCropsAsyncExcludesUnverifiedSuppliers()
    {
        await using var db = _fixture.CreateContext();
        var repository = new SupplierRepository(db);

        var names = (await repository.ListVerifiedWithCropsAsync())
            .Select(r => r.Supplier.Name)
            .ToList();

        Assert.Equal(4, names.Count);
        Assert.DoesNotContain("Tarlac Central Farms", names);
        Assert.DoesNotContain("Benguet Highland Vegetables", names);
    }

    /// <summary>
    /// Each supplier's crops are exactly the distinct crops of its own listings — not the whole
    /// crop table, and not another supplier's. A join written against the wrong key produces one
    /// of those two failures and both look like plausible data.
    /// </summary>
    [Theory]
    [InlineData("Laguna Lakeside Growers", "vegetables")]
    [InlineData("Bataan Rice Growers", "rice")]
    [InlineData("Nueva Ecija Grain Cooperative", "corn,rice")]
    [InlineData("Pangasinan Harvest Traders", "corn,rice")]
    public async Task ListVerifiedWithCropsAsyncDerivesCropsFromListings(string supplierName, string expected)
    {
        await using var db = _fixture.CreateContext();
        var repository = new SupplierRepository(db);

        var row = (await repository.ListVerifiedWithCropsAsync())
            .Single(r => r.Supplier.Name == supplierName);

        Assert.Equal(expected.Split(','), row.Crops);
    }

    /// <summary>
    /// Deduplicated: "Bataan Rice Growers" lists two rice lots and must yield one chip, not two.
    /// And sorted, because the chip order is rendered and an unsorted <c>DISTINCT</c> in Postgres
    /// is free to change between plans.
    /// </summary>
    [Fact]
    public async Task ListVerifiedWithCropsAsyncReturnsDistinctSortedCrops()
    {
        await using var db = _fixture.CreateContext();
        var repository = new SupplierRepository(db);

        foreach (var row in await repository.ListVerifiedWithCropsAsync())
        {
            Assert.Equal(row.Crops.Distinct().Count(), row.Crops.Count);
            Assert.Equal(row.Crops.OrderBy(c => c, StringComparer.Ordinal).ToList(), row.Crops);
        }
    }

    /// <summary>
    /// The left-join case, and the reason the result is assembled from the supplier list rather
    /// than from the crop pairs.
    /// <para>
    /// The seed has no such supplier — all six list something — so this test makes one, which is
    /// also why it is the only test here that writes. The row is removed in a <c>finally</c>:
    /// every other test in this collection counts verified suppliers, and a leaked row would fail
    /// them somewhere else entirely.
    /// </para>
    /// </summary>
    [Fact]
    public async Task ListVerifiedWithCropsAsyncKeepsASupplierWithNoListings()
    {
        await using var db = _fixture.CreateContext();

        var user = new AppUser
        {
            Name = "Test Fixture Farmer",
            Role = UserRole.Farmer,
            Verified = true,
            CreatedAt = PostgresFixture.SeedEpoch,
        };

        var supplier = new Supplier
        {
            AppUser = user,
            Name = "Listingless Verified Co-op",
            Region = "Calamba, Laguna",
            Latitude = 14.0,
            Longitude = 121.0,
            Verified = true,
        };

        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        try
        {
            await using var readContext = _fixture.CreateContext();
            var repository = new SupplierRepository(readContext);

            var rows = await repository.ListVerifiedWithCropsAsync();
            var row = rows.SingleOrDefault(r => r.Supplier.Name == "Listingless Verified Co-op");

            Assert.NotNull(row);
            Assert.Empty(row.Crops);
            Assert.Equal(5, rows.Count);
        }
        finally
        {
            db.Suppliers.Remove(supplier);
            db.AppUsers.Remove(user);
            await db.SaveChangesAsync();
        }
    }
}
