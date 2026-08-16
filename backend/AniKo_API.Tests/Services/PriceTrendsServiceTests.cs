using AniKo_API.Repositories;
using AniKo_API.Services;

namespace AniKo_API.Tests.Services;

public class PriceTrendsServiceTests
{
    private static readonly DateTime Now = new(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc);

    private static readonly string[] AllCrops = ["rice", "corn", "vegetables"];

    private static PriceTrendsService Build(
        IEnumerable<MonthlyCropPrice>? prices = null,
        IEnumerable<string>? crops = null) =>
        new(
            new FakePriceObservationRepository { Rows = [.. prices ?? []] },
            new FakeCropRepository { Names = [.. crops ?? AllCrops] },
            new StubDashboardClock(Now));

    /// <summary>
    /// <b>The off-by-one, asserted on the count and on the endpoints.</b>
    /// </summary>
    /// <remarks>
    /// The current month is one of the requested months, so a three-month window opens two months
    /// back, not three. Subtracting <c>months</c> instead of <c>months - 1</c> produces four
    /// points — a chart that looks entirely healthy while showing a window the label does not
    /// describe, which is why the first and last dates are pinned here and not just the count.
    /// </remarks>
    [Fact]
    public async Task GetAsync_ThreeMonths_YieldsExactlyThreePointsEndingOnTheCurrentMonth()
    {
        var result = await Build().GetAsync(months: 3);

        Assert.Equal(3, result.Points.Count);
        Assert.Equal(["2026-06-01", "2026-07-01", "2026-08-01"], [.. result.Points.Select(p => p.Date)]);
    }

    [Fact]
    public async Task GetAsync_OneMonth_YieldsOnlyTheCurrentMonth()
    {
        var result = await Build().GetAsync(months: 1);

        var point = Assert.Single(result.Points);
        Assert.Equal("2026-08-01", point.Date);
    }

    /// <summary>Twelve is the range selector's longest option; the boundary walks back a year.</summary>
    [Fact]
    public async Task GetAsync_TwelveMonths_CrossesTheYearBoundaryCorrectly()
    {
        var result = await Build().GetAsync(months: 12);

        Assert.Equal(12, result.Points.Count);
        Assert.Equal("2025-09-01", result.Points[0].Date);
        Assert.Equal("2026-08-01", result.Points[^1].Date);
    }

    /// <summary>
    /// The repository is asked for exactly the window that will be rendered. A wider request is
    /// wasted transfer; a narrower one is a leading month that silently renders as all-zero.
    /// </summary>
    [Fact]
    public async Task GetAsync_AsksTheRepositoryForTheFirstMonthOfTheWindow()
    {
        var repository = new FakePriceObservationRepository();

        var service = new PriceTrendsService(
            repository,
            new FakeCropRepository { Names = AllCrops },
            new StubDashboardClock(Now));

        await service.GetAsync(months: 6);

        Assert.Equal(new DateOnly(2026, 3, 1), repository.LastFirstMonth);
    }

    [Fact]
    public async Task GetAsync_PivotsRowsIntoOnePointPerMonthCarryingEveryCrop()
    {
        MonthlyCropPrice[] rows =
        [
            new(new DateOnly(2026, 7, 1), "rice", 51.25m),
            new(new DateOnly(2026, 7, 1), "corn", 23.40m),
            new(new DateOnly(2026, 7, 1), "vegetables", 71.00m),
            new(new DateOnly(2026, 8, 1), "rice", 52.10m),
            new(new DateOnly(2026, 8, 1), "corn", 23.80m),
            new(new DateOnly(2026, 8, 1), "vegetables", 68.50m),
        ];

        var result = await Build(rows).GetAsync(months: 2);

        Assert.Equal(2, result.Points.Count);
        Assert.Equal(51.25m, result.Points[0].Prices["rice"]);
        Assert.Equal(23.40m, result.Points[0].Prices["corn"]);
        Assert.Equal(71.00m, result.Points[0].Prices["vegetables"]);
        Assert.Equal(52.10m, result.Points[1].Prices["rice"]);
        Assert.Equal(23.80m, result.Points[1].Prices["corn"]);
        Assert.Equal(68.50m, result.Points[1].Prices["vegetables"]);
    }

    /// <summary>
    /// <b>Every point carries a key for every crop, whether or not that crop has data.</b>
    /// </summary>
    /// <remarks>
    /// A <c>PricePoint</c> missing a crop key does not draw a gap in that line — Recharts drops
    /// the series from the legend altogether, so one blank month erases the whole line and the
    /// legend changes shape as the user moves the range selector. This is the assertion that
    /// stops the crop axis being derived from the observations.
    /// </remarks>
    [Fact]
    public async Task GetAsync_CropWithNoObservationInAMonth_StillGetsAKey()
    {
        MonthlyCropPrice[] onlyRiceInJuly =
        [
            new(new DateOnly(2026, 7, 1), "rice", 51.25m),
            new(new DateOnly(2026, 8, 1), "rice", 52.10m),
            new(new DateOnly(2026, 8, 1), "corn", 23.80m),
            new(new DateOnly(2026, 8, 1), "vegetables", 68.50m),
        ];

        var result = await Build(onlyRiceInJuly).GetAsync(months: 2);

        Assert.All(result.Points, p => Assert.Equal(AllCrops.Order(), p.Prices.Keys.Order()));

        // Zero, not carried forward from an earlier month and not omitted. No crop trades at
        // ₱0/kg, so the value reads on sight as "no observation" rather than as a market event.
        Assert.Equal(0m, result.Points[0].Prices["corn"]);
        Assert.Equal(0m, result.Points[0].Prices["vegetables"]);
    }

    /// <summary>
    /// A crop that has data before and after a gap does not have the earlier price carried across
    /// it. Carry-forward draws a nicer chart and is indistinguishable, downstream and forever,
    /// from a month in which the price genuinely did not move.
    /// </summary>
    [Fact]
    public async Task GetAsync_GapBetweenTwoObservations_IsNotFilledByCarryingForward()
    {
        MonthlyCropPrice[] withAGap =
        [
            new(new DateOnly(2026, 6, 1), "rice", 51.00m),
            new(new DateOnly(2026, 8, 1), "rice", 53.00m),
        ];

        var result = await Build(withAGap, ["rice"]).GetAsync(months: 3);

        Assert.Equal(51.00m, result.Points[0].Prices["rice"]);
        Assert.Equal(0m, result.Points[1].Prices["rice"]);
        Assert.Equal(53.00m, result.Points[2].Prices["rice"]);
    }

    /// <summary>
    /// A month with no observations at all is still a point on the axis. Deriving the axis from
    /// the data would compress the chart and shift every remaining point sideways.
    /// </summary>
    [Fact]
    public async Task GetAsync_NoObservationsAtAll_StillYieldsTheFullAxisWithEveryCropAtZero()
    {
        var result = await Build().GetAsync(months: 6);

        Assert.Equal(6, result.Points.Count);
        Assert.All(result.Points, p => Assert.Equal(3, p.Prices.Count));
        Assert.All(result.Points, p => Assert.All(p.Prices.Values, v => Assert.Equal(0m, v)));
    }

    /// <summary>
    /// No crops in the reference table is not a state that should occur — crops arrive with the
    /// migration — but it must produce empty dictionaries rather than a null reference.
    /// </summary>
    [Fact]
    public async Task GetAsync_NoCrops_YieldsPointsWithEmptyPriceDictionaries()
    {
        var result = await Build(crops: []).GetAsync(months: 2);

        Assert.Equal(2, result.Points.Count);
        Assert.All(result.Points, p => Assert.Empty(p.Prices));
    }

    /// <summary>
    /// Ordering is part of the contract: a line chart handed unordered points draws a scribble
    /// rather than failing. Asserted even though the loop builds them in order, because the day
    /// someone replaces the loop with a GroupBy is the day it stops being true.
    /// </summary>
    [Fact]
    public async Task GetAsync_PointsAreAscendingByDate()
    {
        var result = await Build().GetAsync(months: 12);

        var dates = result.Points.Select(p => p.Date).ToList();

        Assert.Equal(dates.OrderBy(d => d, StringComparer.Ordinal), dates);
    }
}
