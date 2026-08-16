using AniKo_API.Repositories;

namespace AniKo_API.Tests.Repositories;

/// <summary>
/// <see cref="ListingRepository"/> against the seeded demo dataset.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ListingRepositoryTests
{
    /// <summary>
    /// Six of the twelve seeded lots carry the flag, newest first. Spelled out rather than
    /// derived so that a seed edit that changes which lots are merchandised has to be
    /// acknowledged here — the featured set is a decision, and a test that recomputed it from the
    /// seed could not tell the difference between a deliberate change and an accident.
    /// </summary>
    private static readonly string[] FeaturedNewestFirst =
    [
        "Premium White Rice",
        "Well-Milled Rice",
        "Feed-Grade Yellow Corn",
        "Sweet Corn Kernels",
        "Lakeside Eggplant",
        "Baguio Beans",
    ];

    private readonly PostgresFixture _fixture;

    public ListingRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ListAsyncReturnsEverySeededListing()
    {
        await using var db = _fixture.CreateContext();
        var repository = new ListingRepository(db);

        Assert.Equal(12, (await repository.ListAsync()).Count);
    }

    [Fact]
    public async Task ListFeaturedAsyncReturnsOnlyFeaturedLotsNewestFirst()
    {
        await using var db = _fixture.CreateContext();
        var repository = new ListingRepository(db);

        var rows = await repository.ListFeaturedAsync(50);

        Assert.Equal(FeaturedNewestFirst, rows.Select(r => r.Name).ToArray());
    }

    /// <summary>
    /// The negative half of the filter, and the one worth stating separately: a query missing its
    /// <c>Where</c> would still pass an ordering assertion and still return plausible cards.
    /// </summary>
    [Fact]
    public async Task ListFeaturedAsyncExcludesUnfeaturedLots()
    {
        await using var db = _fixture.CreateContext();
        var repository = new ListingRepository(db);

        var names = (await repository.ListFeaturedAsync(50)).Select(r => r.Name).ToList();

        Assert.DoesNotContain("Dinorado Rice", names);
        Assert.DoesNotContain("Highland Broccoli", names);
        Assert.Equal(6, names.Count);
    }

    /// <summary>
    /// The limit is applied after the ordering, so a smaller limit must return a prefix of the
    /// full result — not an arbitrary subset of it. Taking before sorting is the classic version
    /// of this bug and it only shows up once the table has more rows than the limit.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    public async Task ListFeaturedAsyncRespectsTheLimit(int limit)
    {
        await using var db = _fixture.CreateContext();
        var repository = new ListingRepository(db);

        var rows = await repository.ListFeaturedAsync(limit);

        Assert.Equal(limit, rows.Count);
        Assert.Equal(FeaturedNewestFirst.Take(limit), rows.Select(r => r.Name));
    }

    /// <summary>
    /// Crop name is the lowercase series key, not the trade name on the sack; supplier name and
    /// region arrive through the join. All three are what the card renders.
    /// </summary>
    [Fact]
    public async Task ListFeaturedAsyncResolvesCropAndSupplierInSql()
    {
        await using var db = _fixture.CreateContext();
        var repository = new ListingRepository(db);

        var newest = (await repository.ListFeaturedAsync(1)).Single();

        Assert.Equal("Premium White Rice", newest.Name);
        Assert.Equal("rice", newest.CropName);
        Assert.Equal("Bataan Rice Growers", newest.SupplierName);
        Assert.Equal("Balanga, Bataan", newest.Region);
        Assert.Equal("A", newest.Grade);
        Assert.True(newest.Verified);
        Assert.Equal(24_000, newest.VolumeKg);
        Assert.Equal(500, newest.MinimumOrderKg);
        Assert.Equal(58.50m, newest.PricePerKg);
    }

    /// <summary>
    /// A featured lot from an unverified supplier keeps <c>Verified = false</c>, which is the case
    /// that distinguishes reading the listing's own column from joining to the supplier's.
    /// </summary>
    [Fact]
    public async Task ListFeaturedAsyncReportsVerificationFromTheListing()
    {
        await using var db = _fixture.CreateContext();
        var repository = new ListingRepository(db);

        var rows = await repository.ListFeaturedAsync(50);

        // Baguio Beans belongs to Benguet Highland Vegetables, which is not verified.
        var unverified = rows.Single(r => r.Name == "Baguio Beans");
        Assert.False(unverified.Verified);
    }
}
