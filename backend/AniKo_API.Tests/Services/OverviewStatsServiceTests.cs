using AniKo_API.Models;
using AniKo_API.Repositories;
using AniKo_API.Services;

namespace AniKo_API.Tests.Services;

/// <summary>
/// The stat tiles are entirely defined relative to "now", so every case here pins the clock and
/// places rows at known offsets from it. Nothing in this file would be assertable against
/// <c>DateTime.UtcNow</c>.
/// </summary>
public class OverviewStatsServiceTests
{
    /// <summary>Mid-month on purpose: a boundary bug that only shows up on the 1st stays hidden.</summary>
    private static readonly DateTime Now = new(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc);

    private static OverviewStatsService Build(
        IEnumerable<OrderStatsRow>? orders = null,
        IEnumerable<MonthlyCropPrice>? prices = null) =>
        new(
            new FakeOrderRepository { StatsRows = [.. orders ?? []] },
            new FakePriceObservationRepository { Rows = [.. prices ?? []] },
            new StubDashboardClock(Now));

    private static decimal ValueOf(AniKo_API.Dtos.OverviewStatsDto dto, string key) =>
        dto.Stats.Single(s => s.Key == key).Value;

    private static decimal DeltaOf(AniKo_API.Dtos.OverviewStatsDto dto, string key) =>
        dto.Stats.Single(s => s.Key == key).DeltaPercent;

    /// <summary>
    /// Three orders inside the trailing 30 days and two in the 30 before that, chosen so that
    /// every tile's current and prior figure differs from every other tile's — a fixture where
    /// two numbers coincide cannot distinguish "correct" from "read the wrong column".
    /// </summary>
    private static readonly OrderStatsRow[] HappyPathOrders =
    [
        Rows.Order(Now.AddDays(-5), supplierId: 1, quantityKg: 100, pricePerKg: 50m, status: OrderStatus.Confirmed),
        Rows.Order(Now.AddDays(-10), supplierId: 2, quantityKg: 200, pricePerKg: 25m, status: OrderStatus.Processing),
        Rows.Order(Now.AddDays(-20), supplierId: 1, quantityKg: 100, pricePerKg: 10m, status: OrderStatus.Delivered),

        Rows.Order(Now.AddDays(-35), supplierId: 3, quantityKg: 100, pricePerKg: 50m, status: OrderStatus.Confirmed),
        Rows.Order(Now.AddDays(-45), supplierId: 3, quantityKg: 50, pricePerKg: 20m, status: OrderStatus.Delivered),

        // Outside both windows. Present so that a service which forgot to bound the fetch, or
        // which put everything it received into the current window, fails here.
        Rows.Order(Now.AddDays(-70), supplierId: 4, quantityKg: 9_999, pricePerKg: 999m),
    ];

    private static readonly MonthlyCropPrice[] HappyPathPrices =
    [
        new(new DateOnly(2026, 8, 1), "rice", 60m),
        new(new DateOnly(2026, 8, 1), "corn", 20m),
        new(new DateOnly(2026, 7, 1), "rice", 40m),
        new(new DateOnly(2026, 7, 1), "corn", 24m),
    ];

    [Fact]
    public async Task GetAsync_EmitsExactlyTheFourKeysInStatKeysOrder()
    {
        var result = await Build(HappyPathOrders, HappyPathPrices).GetAsync();

        Assert.Equal(StatKeys.All, [.. result.Stats.Select(s => s.Key)]);
    }

    [Fact]
    public async Task GetAsync_EmptyDatabase_StillEmitsAllFourTilesAtZero()
    {
        // The tiles live in a fixed four-column grid. Dropping one because its table is empty
        // does not leave a gap the layout absorbs, it leaves a hole — and this is the state every
        // freshly migrated, unseeded database is in.
        var result = await Build().GetAsync();

        Assert.Equal(StatKeys.All, [.. result.Stats.Select(s => s.Key)]);
        Assert.All(result.Stats, s => Assert.Equal(0m, s.Value));
        Assert.All(result.Stats, s => Assert.Equal(0m, s.DeltaPercent));
    }

    [Fact]
    public async Task GetAsync_ActiveOrders_CountsEverythingThatIsNotDelivered()
    {
        var result = await Build(HappyPathOrders, HappyPathPrices).GetAsync();

        // Two of the three current-window orders are not Delivered.
        Assert.Equal(2m, ValueOf(result, StatKeys.ActiveOrders));

        // One of the two prior-window orders is not Delivered, so 1 → 2 is +100%.
        Assert.Equal(100m, DeltaOf(result, StatKeys.ActiveOrders));
    }

    [Fact]
    public async Task GetAsync_Spend_IsQuantityTimesPriceAcrossEveryOrderInTheWindow()
    {
        var result = await Build(HappyPathOrders, HappyPathPrices).GetAsync();

        // Delivered orders still count towards spend — money spent is money spent, regardless of
        // whether the sacks have arrived.
        Assert.Equal(11_000m, ValueOf(result, StatKeys.Spend));

        // Prior window is 5,000 + 1,000 = 6,000; (11000 - 6000) / 6000 = 83.33…%, one decimal.
        Assert.Equal(83.3m, DeltaOf(result, StatKeys.Spend));
    }

    [Fact]
    public async Task GetAsync_Suppliers_CountsDistinctSuppliersNotOrders()
    {
        var result = await Build(HappyPathOrders, HappyPathPrices).GetAsync();

        // Three current-window orders, but supplier 1 placed two of them.
        Assert.Equal(2m, ValueOf(result, StatKeys.Suppliers));
        Assert.Equal(100m, DeltaOf(result, StatKeys.Suppliers));
    }

    [Fact]
    public async Task GetAsync_AveragePrice_MeansAcrossCropsForTheLatestMonthAgainstTheOneBefore()
    {
        var result = await Build(HappyPathOrders, HappyPathPrices).GetAsync();

        // August: (60 + 20) / 2 = 40. July: (40 + 24) / 2 = 32. (40 - 32) / 32 = +25%.
        Assert.Equal(40m, ValueOf(result, StatKeys.AveragePrice));
        Assert.Equal(25m, DeltaOf(result, StatKeys.AveragePrice));
    }

    [Fact]
    public async Task GetAsync_FetchesOneSixtyDayRangeRatherThanTwoThirtyDayOnes()
    {
        var orderRepository = new FakeOrderRepository { StatsRows = HappyPathOrders };

        var service = new OverviewStatsService(
            orderRepository,
            new FakePriceObservationRepository(),
            new StubDashboardClock(Now));

        await service.GetAsync();

        // Both windows come out of one call. Two calls would leave a gap between them in which an
        // order placed mid-flight is counted twice or not at all.
        Assert.Equal(Now.AddDays(-60), orderRepository.LastSince);
    }

    /// <summary>
    /// <b>The zero-prior case, tested because the arithmetic has no answer.</b>
    /// </summary>
    /// <remarks>
    /// A buyer's first month on the platform has orders in the current window and none before it,
    /// so <c>prior</c> is zero for three of the four tiles at once. The service emits 0, meaning
    /// "no baseline", rather than 100, which would render four confident green "+100%" chips
    /// asserting a doubling that never happened. The important half of this assertion is that it
    /// is not <c>+100</c> and not an exception.
    /// </remarks>
    [Fact]
    public async Task GetAsync_PriorPeriodEmpty_EmitsZeroDeltaRatherThanDividingByZero()
    {
        OrderStatsRow[] currentOnly =
        [
            Rows.Order(Now.AddDays(-3), supplierId: 1, quantityKg: 500, pricePerKg: 58.50m),
            Rows.Order(Now.AddDays(-4), supplierId: 2, quantityKg: 300, pricePerKg: 21.80m),
        ];

        var result = await Build(currentOnly, [new(new DateOnly(2026, 8, 1), "rice", 55m)]).GetAsync();

        Assert.Equal(2m, ValueOf(result, StatKeys.ActiveOrders));
        Assert.Equal(2m, ValueOf(result, StatKeys.Suppliers));
        Assert.Equal(35_790m, ValueOf(result, StatKeys.Spend));
        Assert.Equal(55m, ValueOf(result, StatKeys.AveragePrice));

        Assert.All(result.Stats, s => Assert.Equal(0m, s.DeltaPercent));
    }

    /// <summary>
    /// The mirror of the case above: a current period of nothing against a real prior period is
    /// -100%, which is a genuine figure and must not be swallowed by the zero guard.
    /// </summary>
    [Fact]
    public async Task GetAsync_CurrentPeriodEmptyAgainstARealPrior_EmitsMinusOneHundred()
    {
        OrderStatsRow[] priorOnly =
        [
            Rows.Order(Now.AddDays(-40), supplierId: 1, quantityKg: 100, pricePerKg: 50m),
        ];

        var result = await Build(priorOnly).GetAsync();

        Assert.Equal(0m, ValueOf(result, StatKeys.ActiveOrders));
        Assert.Equal(-100m, DeltaOf(result, StatKeys.ActiveOrders));
        Assert.Equal(-100m, DeltaOf(result, StatKeys.Spend));
        Assert.Equal(-100m, DeltaOf(result, StatKeys.Suppliers));
    }

    /// <summary>
    /// Only one month of price observations exists, so there is nothing to compare against. The
    /// tile must still show the price it does know rather than blanking the whole thing.
    /// </summary>
    [Fact]
    public async Task GetAsync_SingleMonthOfPrices_ShowsThePriceWithAZeroDelta()
    {
        MonthlyCropPrice[] oneMonth =
        [
            new(new DateOnly(2026, 8, 1), "rice", 50m),
            new(new DateOnly(2026, 8, 1), "corn", 30m),
        ];

        var result = await Build(prices: oneMonth).GetAsync();

        Assert.Equal(40m, ValueOf(result, StatKeys.AveragePrice));
        Assert.Equal(0m, DeltaOf(result, StatKeys.AveragePrice));
    }

    /// <summary>
    /// Observations are published with a lag, so the newest month present may not be the current
    /// calendar month. The tile follows the data rather than the calendar — and it compares
    /// against the month immediately before the newest one, not merely the next group down.
    /// </summary>
    [Fact]
    public async Task GetAsync_LaggingObservations_UsesTheLatestMonthPresentNotTheCalendarMonth()
    {
        MonthlyCropPrice[] laggingByOneMonth =
        [
            new(new DateOnly(2026, 7, 1), "rice", 44m),
            new(new DateOnly(2026, 6, 1), "rice", 40m),
        ];

        var result = await Build(prices: laggingByOneMonth).GetAsync();

        Assert.Equal(44m, ValueOf(result, StatKeys.AveragePrice));
        Assert.Equal(10m, DeltaOf(result, StatKeys.AveragePrice));
    }
}
