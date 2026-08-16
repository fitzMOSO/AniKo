using AniKo_API.Dtos;
using AniKo_API.Models;
using AniKo_API.Repositories;

namespace AniKo_API.Services;

/// <summary>
/// Builds the four overview tiles: a current figure and a change against the previous period.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="TimeProvider"/> rather than <c>DateTime.UtcNow</c>.</b> Every figure here is
/// defined relative to "now", so a service that reads the ambient clock is a service whose output
/// cannot be asserted — a test would have to seed rows relative to the real clock and would then
/// fail the first time it ran across a window boundary. Injecting the clock turns "the last 30
/// days" into an argument, and the tests below pin it.
/// </para>
/// <para>
/// <b>Always four tiles, in <see cref="StatKeys.All"/> order.</b> The dashboard lays these out in
/// a fixed four-column grid and renders them in received order. Omitting a tile because its
/// underlying table is empty does not produce an empty tile, it produces a three-column row with
/// a hole in it, on first run against a database that has not been seeded yet.
/// </para>
/// </remarks>
public sealed class OverviewStatsService(
    IOrderRepository orders,
    IPriceObservationRepository priceObservations,
    TimeProvider timeProvider) : IOverviewStatsService
{
    /// <summary>
    /// The length of both the current window and the comparison window immediately before it.
    /// </summary>
    /// <remarks>
    /// 30 days, not "this calendar month". A calendar month makes the delta meaningless for the
    /// first few days of a month — two days of orders compared against thirty-one is a -93% that
    /// says nothing about the business — and it makes the tile lurch every time the month turns
    /// over. A trailing window is the same width every day of the year.
    /// </remarks>
    private const int WindowDays = 30;

    /// <summary>
    /// How far back the price lookup reaches, in whole months including the current one.
    /// </summary>
    /// <remarks>
    /// Three, where two would be arithmetically sufficient. Market price observations are
    /// published with a lag, so on any day before this month's figures land, a strict two-month
    /// window contains one usable month and the tile shows a price with no comparison. The third
    /// month costs a handful of rows and absorbs one month of lag.
    /// </remarks>
    private const int PriceLookbackMonths = 3;

    /// <summary>
    /// Decimal places on <c>deltaPercent</c>.
    /// </summary>
    /// <remarks>
    /// One. The frontend renders this in a small chip as "+12.5%", so two places is precision the
    /// chip has no room for and the seeded data does not support, while zero places rounds a real
    /// 0.4% move to "0%" — which reads as "nothing happened" rather than "barely anything
    /// happened", and is the one misreading a delta chip exists to prevent.
    /// </remarks>
    private const int DeltaDecimals = 1;

    /// <summary>Decimal places on the money-valued tiles. PHP has centavos; nothing has mills.</summary>
    private const int MoneyDecimals = 2;

    public async Task<OverviewStatsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var currentStart = now.AddDays(-WindowDays);
        var priorStart = now.AddDays(-WindowDays * 2);

        // One fetch covering both windows, split in memory. Two fetches would double the round
        // trips for one contiguous range and would leave a gap between them in which an order
        // placed mid-flight lands in both windows or in neither — see IOrderRepository.
        var rows = await orders.ListSinceAsync(priorStart, cancellationToken).ConfigureAwait(false);

        var current = rows.Where(r => r.CreatedAt >= currentStart).ToList();
        var prior = rows.Where(r => r.CreatedAt >= priorStart && r.CreatedAt < currentStart).ToList();

        var (currentAvgPrice, priorAvgPrice) =
            await AveragePricesAsync(now, cancellationToken).ConfigureAwait(false);

        // Keyed rather than positional so the emission loop below can assert the order comes from
        // StatKeys.All and not from the order the values happen to be computed in.
        var values = new Dictionary<string, (decimal Current, decimal Prior)>(StringComparer.Ordinal)
        {
            [StatKeys.ActiveOrders] = (ActiveOrders(current), ActiveOrders(prior)),
            [StatKeys.Spend] = (Spend(current), Spend(prior)),
            [StatKeys.Suppliers] = (DistinctSuppliers(current), DistinctSuppliers(prior)),
            [StatKeys.AveragePrice] = (currentAvgPrice, priorAvgPrice),
        };

        var stats = StatKeys.All
            .Select(key =>
            {
                var (value, priorValue) = values[key];
                return new OverviewStatDto(key, value, DeltaPercent(value, priorValue));
            })
            .ToList();

        return new OverviewStatsDto(stats);
    }

    /// <summary>
    /// "Active" is defined by exclusion — anything that is not <see cref="OrderStatus.Delivered"/>
    /// — rather than by listing the three live statuses. A fifth status added later is far more
    /// likely to be another in-flight state than another terminal one, and the inclusive form
    /// would silently stop counting it.
    /// </summary>
    private static decimal ActiveOrders(IEnumerable<OrderStatsRow> rows) =>
        rows.Count(r => r.Status != OrderStatus.Delivered);

    private static decimal Spend(IEnumerable<OrderStatsRow> rows) =>
        rows.Sum(r => r.QuantityKg * r.PricePerKg);

    private static decimal DistinctSuppliers(IEnumerable<OrderStatsRow> rows) =>
        rows.Select(r => r.SupplierId).Distinct().Count();

    /// <summary>
    /// The mean observed price across every crop for the latest month that has data, and for the
    /// month before it.
    /// </summary>
    /// <remarks>
    /// The latest month is discovered from the rows rather than assumed to be the current
    /// calendar month, for the publication-lag reason on <see cref="PriceLookbackMonths"/>.
    /// <para>
    /// A plain mean across crops, deliberately unweighted. It mixes ₱21/kg corn with ₱118/kg
    /// broccoli, so it is not a price anybody pays — it is an index, and the tile's job is to
    /// show which way it moved. Weighting it by traded volume would make it a truer figure and
    /// would require joining orders to observations by crop and month, which is a different
    /// query and a different tile.
    /// </para>
    /// </remarks>
    private async Task<(decimal Current, decimal Prior)> AveragePricesAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        var firstMonth = new DateOnly(now.Year, now.Month, 1).AddMonths(-(PriceLookbackMonths - 1));

        var rows = await priceObservations
            .ListMonthlyAveragesAsync(firstMonth, cancellationToken)
            .ConfigureAwait(false);

        if (rows.Count == 0)
        {
            return (0m, 0m);
        }

        var byMonth = rows
            .GroupBy(r => r.Month)
            .OrderByDescending(g => g.Key)
            .ToList();

        var latest = Round(byMonth[0].Average(r => r.AveragePricePerKg), MoneyDecimals);

        // The month *before the latest one that has data*, not "the second group in the list".
        // Those differ when a month is missing entirely from the observations, and comparing
        // August against June while labelling it a month-on-month change is a wrong number rather
        // than a missing one.
        var previousMonth = byMonth[0].Key.AddMonths(-1);
        var previous = byMonth.FirstOrDefault(g => g.Key == previousMonth);

        var prior = previous is null
            ? 0m
            : Round(previous.Average(r => r.AveragePricePerKg), MoneyDecimals);

        return (latest, prior);
    }

    /// <summary>
    /// Percentage change from <paramref name="prior"/> to <paramref name="current"/>.
    /// </summary>
    /// <remarks>
    /// <b>Zero prior emits zero, and that is a choice worth defending.</b> Growth from a base of
    /// nothing has no percentage — it is a division by zero, not a large number — so every
    /// available answer is a fiction and the question is which fiction misleads least.
    /// <list type="bullet">
    /// <item>
    /// <c>100</c> is the tempting one and the worst one. It renders as a confident green "+100%"
    /// chip beside a real figure, which asserts a doubling that did not happen; a buyer's first
    /// month on the platform would show four tiles all claiming +100% growth.
    /// </item>
    /// <item>
    /// <c>0</c> renders as a neutral "0%" chip, whose meaning — "no comparison available" — is
    /// the closest true statement the wire format can carry. <c>DeltaPercent</c> is a non-nullable
    /// <c>decimal</c> on the DTO, so "unknown" is not expressible; if it ever needs to be, the fix
    /// is <c>decimal?</c> plus a frontend em-dash, not a sentinel value here.
    /// </item>
    /// </list>
    /// Note that this deliberately does not special-case "prior is zero and current is positive".
    /// Doing so would emit +100% for exactly the empty-database case above, which is the case that
    /// most needs to read as "no baseline".
    /// </remarks>
    private static decimal DeltaPercent(decimal current, decimal prior)
    {
        if (prior == 0m)
        {
            return 0m;
        }

        return Round((current - prior) / prior * 100m, DeltaDecimals);
    }

    /// <summary>
    /// <see cref="MidpointRounding.AwayFromZero"/>, not the .NET default of ToEven. Banker's
    /// rounding is right for repeated sums that must not drift; these are single display figures,
    /// and 12.45 rendering as "12.4" is the kind of thing that gets reported as a bug.
    /// </summary>
    private static decimal Round(decimal value, int decimals) =>
        Math.Round(value, decimals, MidpointRounding.AwayFromZero);
}
