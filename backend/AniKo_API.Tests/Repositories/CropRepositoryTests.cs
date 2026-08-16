using AniKo_API.Data.Seed;
using AniKo_API.Repositories;

namespace AniKo_API.Tests.Repositories;

/// <summary>
/// <see cref="CropRepository"/>. Crops arrive with the migration rather than with the demo
/// seeder, so these tests are also the check that <c>HasData</c> actually landed — a database
/// created with <c>EnsureCreated</c> instead of <c>Migrate</c> would have the table and none of
/// the rows.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CropRepositoryTests
{
    private readonly PostgresFixture _fixture;

    public CropRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ListNamesAsyncReturnsEveryCropSorted()
    {
        await using var db = _fixture.CreateContext();
        var repository = new CropRepository(db);

        var names = await repository.ListNamesAsync();

        Assert.Equal(["corn", "rice", "vegetables"], names);
    }

    /// <summary>
    /// The full crop set, not the crops that happen to have observations — which is the whole
    /// reason this method exists rather than the trends query deriving its series list.
    /// </summary>
    [Fact]
    public async Task ListNamesAsyncMatchesTheReferenceData()
    {
        await using var db = _fixture.CreateContext();
        var repository = new CropRepository(db);

        Assert.Equal(
            ReferenceData.Crops.Select(c => c.Name).OrderBy(n => n, StringComparer.Ordinal).ToList(),
            await repository.ListNamesAsync());
    }

    [Fact]
    public async Task FindAsyncReturnsTheCropWithThePinnedId()
    {
        await using var db = _fixture.CreateContext();
        var repository = new CropRepository(db);

        var rice = await repository.FindAsync(ReferenceData.CropIds.Rice);

        Assert.NotNull(rice);
        Assert.Equal("rice", rice.Name);
        Assert.Equal("kg", rice.Unit);
    }
}
