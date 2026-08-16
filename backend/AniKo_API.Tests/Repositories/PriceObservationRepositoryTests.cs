using AniKo_API.Data.Seed;
using AniKo_API.Models;
using AniKo_API.Repositories;

namespace AniKo_API.Tests.Repositories;

/// <summary>
/// <see cref="PriceObservationRepository"/>: the one query that aggregates.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PriceObservationRepositoryTests
{
    private readonly PostgresFixture _fixture;

    public PriceObservationRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ListAsyncReturnsEverySeededObservation()
    {
        await using var db = _fixture.CreateContext();
        var repository = new PriceObservationRepository(db);

        Assert.Equal(36, (await repository.ListAsync()).Count);
    }

    /// <summary>
    /// Three crops over twelve months, and nothing collapsed: a <c>GroupBy</c> that keyed on the
    /// month alone would return twelve rows of a meaningless cross-crop mean and would still look
    /// like a working chart.
    /// </summary>
    [Fact]
    public async Task ListMonthlyAveragesAsyncReturnsOneRowPerCropPerMonth()
    {
        await using var db = _fixture.CreateContext();
        var repository = new PriceObservationRepository(db);

        var rows = await repository.ListMonthlyAveragesAsync(DemoDataSeeder.FirstHistoryMonth);

        Assert.Equal(36, rows.Count);
        Assert.Equal(12, rows.Select(r => r.Month).Distinct().Count());
        Assert.Equal(3, rows.Select(r => r.CropName).Distinct().Count());
        Assert.Equal(36, rows.Select(r => (r.Month, r.CropName)).Distinct().Count());
    }

    /// <summary>
    /// Ascending by month, because the chart plots the sequence in the order it is given and a
    /// line drawn over shuffled x-values is a scribble, not an error.
    /// </summary>
    [Fact]
    public async Task ListMonthlyAveragesAsyncOrdersByMonthAscending()
    {
        await using var db = _fixture.CreateContext();
        var repository = new PriceObservationRepository(db);

        var months = (await repository.ListMonthlyAveragesAsync(DemoDataSeeder.FirstHistoryMonth))
            .Select(r => r.Month)
            .ToList();

        Assert.Equal(months.OrderBy(m => m).ToList(), months);
    }

    /// <summary>
    /// The <c>firstMonth</c> filter is inclusive of its own month and drops everything before it.
    /// </summary>
    [Fact]
    public async Task ListMonthlyAveragesAsyncFiltersFromTheGivenMonthInclusive()
    {
        await using var db = _fixture.CreateContext();
        var repository = new PriceObservationRepository(db);

        var from = DemoDataSeeder.FirstHistoryMonth.AddMonths(9);
        var rows = await repository.ListMonthlyAveragesAsync(from);

        // Three remaining months × three crops.
        Assert.Equal(9, rows.Count);
        Assert.Equal(from, rows.Select(r => r.Month).Min());
        Assert.All(rows, r => Assert.True(r.Month >= from));
    }

    /// <summary>
    /// With one region per crop, the "average" is the observation itself — which is worth
    /// asserting because it pins the values to the seed, but is not evidence that any averaging
    /// happens. See <see cref="ListMonthlyAveragesAsyncAveragesAcrossRegions"/> for that.
    /// </summary>
    [Fact]
    public async Task ListMonthlyAveragesAsyncMatchesTheSeededSeriesWhereThereIsOneRegion()
    {
        await using var db = _fixture.CreateContext();
        var repository = new PriceObservationRepository(db);

        var expected = DemoDataSeeder.BuildPriceObservations()
            .ToDictionary(o => (o.CropId, o.Month), o => o.PricePerKg);

        var cropIdsByName = ReferenceData.Crops.ToDictionary(c => c.Name, c => c.Id);

        foreach (var row in await repository.ListMonthlyAveragesAsync(DemoDataSeeder.FirstHistoryMonth))
        {
            Assert.Equal(expected[(cropIdsByName[row.CropName], row.Month)], row.AveragePricePerKg);
        }
    }

    /// <summary>
    /// The behaviour the record's name claims, tested with actual arithmetic.
    /// <para>
    /// The seed cannot demonstrate it: <see cref="DemoDataSeeder"/> writes exactly one region per
    /// crop, deliberately, so every seeded group has a single member and <c>AVG</c> over it is
    /// indistinguishable from <c>MIN</c>, <c>MAX</c>, or from no aggregation at all. So this test
    /// adds a second region for one crop-month, checks the returned figure is the mean of the two
    /// and not either of them, and removes the row again. The offset is chosen even so the
    /// expected mean is exact in decimal and the assertion is not a tolerance comparison.
    /// </para>
    /// </summary>
    [Fact]
    public async Task ListMonthlyAveragesAsyncAveragesAcrossRegions()
    {
        await using var db = _fixture.CreateContext();
        var repository = new PriceObservationRepository(db);

        var month = DemoDataSeeder.FirstHistoryMonth.AddMonths(4);
        var seeded = DemoDataSeeder.BuildPriceObservations()
            .Single(o => o.CropId == ReferenceData.CropIds.Rice && o.Month == month)
            .PricePerKg;

        var secondRegion = new PriceObservation
        {
            CropId = ReferenceData.CropIds.Rice,
            Region = ReferenceData.Regions[1],
            Month = month,
            PricePerKg = seeded + 10.00m,
        };

        db.PriceObservations.Add(secondRegion);
        await db.SaveChangesAsync();

        try
        {
            await using var readContext = _fixture.CreateContext();
            var readRepository = new PriceObservationRepository(readContext);

            var row = (await readRepository.ListMonthlyAveragesAsync(DemoDataSeeder.FirstHistoryMonth))
                .Single(r => r.CropName == "rice" && r.Month == month);

            Assert.Equal(seeded + 5.00m, row.AveragePricePerKg);
            Assert.NotEqual(seeded, row.AveragePricePerKg);

            // Only that one group moved — averaging must not bleed across months or crops.
            var neighbour = (await readRepository.ListMonthlyAveragesAsync(DemoDataSeeder.FirstHistoryMonth))
                .Single(r => r.CropName == "rice" && r.Month == month.AddMonths(1));

            var expectedNeighbour = DemoDataSeeder.BuildPriceObservations()
                .Single(o => o.CropId == ReferenceData.CropIds.Rice && o.Month == month.AddMonths(1))
                .PricePerKg;

            Assert.Equal(expectedNeighbour, neighbour.AveragePricePerKg);
        }
        finally
        {
            db.PriceObservations.Remove(secondRegion);
            await db.SaveChangesAsync();
        }

        // And the extra row really is gone, so the tests that follow see the seeded dataset.
        await using var afterContext = _fixture.CreateContext();
        Assert.Equal(36, (await new PriceObservationRepository(afterContext).ListAsync()).Count);
    }
}
