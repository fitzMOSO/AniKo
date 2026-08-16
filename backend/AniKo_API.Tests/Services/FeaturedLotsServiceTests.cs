using AniKo_API.Repositories;
using AniKo_API.Services;

namespace AniKo_API.Tests.Services;

public class FeaturedLotsServiceTests
{
    private static (FeaturedLotsService Service, FakeListingRepository Repository) Build(
        params FeaturedListingRow[] rows)
    {
        var repository = new FakeListingRepository { Rows = rows };
        return (new FeaturedLotsService(repository), repository);
    }

    /// <summary>
    /// The repository owns "featured only, newest first"; the service must not re-sort. Given
    /// three rows in the repository's order, they come out in that order — an alphabetical or
    /// price sort creeping in here would silently override the ordering the SQL established.
    /// </summary>
    [Fact]
    public async Task GetAsync_PreservesTheRepositoryOrdering()
    {
        var (service, _) = Build(
            Rows.Featured(11, "Premium White Rice"),
            Rows.Featured(3, "Baguio Beans"),
            Rows.Featured(7, "Feed-Grade Yellow Corn"));

        var result = await service.GetAsync(limit: 10);

        Assert.Equal(
            ["Premium White Rice", "Baguio Beans", "Feed-Grade Yellow Corn"],
            [.. result.Lots.Select(l => l.Name)]);
    }

    [Fact]
    public async Task GetAsync_PassesTheLimitThroughToTheRepository()
    {
        var (service, repository) = Build(
            Rows.Featured(1, "One"),
            Rows.Featured(2, "Two"),
            Rows.Featured(3, "Three"));

        var result = await service.GetAsync(limit: 2);

        // Capped in SQL, not in memory. A service that fetched everything and trimmed afterwards
        // would pass the count assertion below and still transfer the whole table.
        Assert.Equal(2, repository.LastLimit);
        Assert.Equal(2, result.Lots.Count);
    }

    [Fact]
    public async Task GetAsync_NoFeaturedListings_YieldsAnEmptyListRatherThanNull()
    {
        var (service, _) = Build();

        var result = await service.GetAsync(limit: 10);

        Assert.NotNull(result.Lots);
        Assert.Empty(result.Lots);
    }

    /// <summary>
    /// The two conversions the mapper exists for: the int id becomes a string, and
    /// <c>MinimumOrderKg</c> becomes <c>minOrderKg</c>. Both are silent failures — a numeric id
    /// survives JSON and dies at the frontend's first <c>===</c>.
    /// </summary>
    [Fact]
    public async Task GetAsync_MapsThroughDashboardMappers()
    {
        var (service, _) = Build(Rows.Featured(42, "Premium White Rice"));

        var lot = Assert.Single((await service.GetAsync(limit: 10)).Lots);

        Assert.Equal("42", lot.Id);
        Assert.Equal("rice", lot.Crop);
        Assert.Equal("Bataan Rice Growers", lot.Supplier);
        Assert.Equal(500, lot.MinOrderKg);
        Assert.Equal(58.50m, lot.PricePerKg);
    }
}
