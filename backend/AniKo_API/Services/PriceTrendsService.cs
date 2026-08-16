using AniKo_API.Dtos;
using AniKo_API.Mapping;
using AniKo_API.Repositories;

namespace AniKo_API.Services;

/// <summary>
/// Pivots monthly price observations into the row-per-date shape the chart consumes.
/// </summary>
/// <remarks>
/// <para>
/// Two axes are constructed here, and both are constructed from something other than the data.
/// The <b>month axis</b> comes from the clock and <c>months</c>, not from the months present in
/// the observations; the <b>crop axis</b> comes from <see cref="ICropRepository.ListNamesAsync"/>,
/// not from the crops present in the observations. Deriving either from the data is the same bug
/// twice: a gap in the data becomes a gap in the chart's structure rather than a visible gap in a
/// line, so a missing month silently compresses the x-axis and a crop with no recent data
/// silently disappears from the legend. Both look like a working chart.
/// </para>
/// <para>
/// The month axis comes from <see cref="IDashboardClock"/>, not the wall clock. A window
/// counted back from a wall-clock "now" over data that stops at a fixed epoch produces a chart
/// whose right-hand months are all <see cref="MissingPrice"/> — every series pinned to the floor,
/// which reads as a market crash rather than as an empty window.
/// </para>
/// </remarks>
public sealed class PriceTrendsService(
    IPriceObservationRepository priceObservations,
    ICropRepository crops,
    IDashboardClock clock) : IPriceTrendsService
{
    /// <summary>
    /// What a crop gets in a month it has no observation for.
    /// </summary>
    /// <remarks>
    /// Zero, and the alternatives lost for concrete reasons rather than on taste.
    /// <list type="bullet">
    /// <item>
    /// <b>Omit the key.</b> Ruled out by the contract — see <see cref="ICropRepository"/>. A
    /// <c>PricePoint</c> missing a crop key makes Recharts drop that series from the legend for
    /// the whole range, so one blank month erases a whole line.
    /// </item>
    /// <item>
    /// <b>Carry the previous month forward.</b> Draws the prettiest chart and tells the biggest
    /// lie: the result is indistinguishable, downstream and forever, from a month in which the
    /// price genuinely did not move. A chart that invents observations is worse than one with a
    /// visible hole in it.
    /// </item>
    /// <item>
    /// <b>Zero.</b> Kept because no crop trades at ₱0/kg, so the value is self-evidently not a
    /// price — the line drops to the floor, which reads on sight as missing data rather than as a
    /// market event, and it is assertable in a test.
    /// </item>
    /// </list>
    /// The genuinely correct answer is <c>decimal?</c> on <see cref="PricePointDto.Prices"/> plus
    /// Recharts' <c>connectNulls</c>, which is a change to the wire contract shared with the
    /// frontend, not a change a service may make on its own.
    /// </remarks>
    private const decimal MissingPrice = 0m;

    public async Task<PriceTrendsDto> GetAsync(int months, CancellationToken cancellationToken = default)
    {
        // months is already validated to [1, 24]; clamping here would turn a frontend bug into a
        // chart quietly showing a different window than the one the user selected. See
        // IPriceTrendsService.
        // The price series' own latest month, not the order clock's. These are seeded on
        // different anchors and sit one month apart in production; using the order clock here
        // opened the window one month early, which rendered as a leading all-zero column that
        // MissingPrice's own doc comment describes as reading like missing data.
        //
        // Falls back to the clock only when there are no observations at all. In that case every
        // point is MissingPrice regardless, so the window's position is unobservable — but a
        // window anchored on *something* keeps the point count honest.
        var latestObserved = await priceObservations
            .LatestMonthAsync(cancellationToken)
            .ConfigureAwait(false);

        var currentMonth = latestObserved ?? DateOnlyFromClock(
            await clock.ReferenceNowAsync(cancellationToken).ConfigureAwait(false));

        // The off-by-one that matters: the current month is one of the `months` points, so the
        // window opens `months - 1` months back. Subtracting `months` yields months+1 points and
        // an extra leading column that the "last 3 months" label does not describe.
        var firstMonth = currentMonth.AddMonths(-(months - 1));

        // Sequential, not Task.WhenAll. These two repositories share one scoped DbContext, and
        // EF Core throws on a second operation started before the first completes. The obvious
        // "optimisation" here is a runtime exception under load and a passing test suite.
        var rows = await priceObservations
            .ListMonthlyAveragesAsync(firstMonth, cancellationToken)
            .ConfigureAwait(false);

        var cropNames = await crops.ListNamesAsync(cancellationToken).ConfigureAwait(false);

        // Ordinal comparison: crop names are lookup keys shared with the frontend's SeriesKey
        // union, not prose, so a culture-aware comparison here could collide two distinct keys.
        var pricesByMonth = rows
            .GroupBy(r => r.Month)
            .ToDictionary(
                g => g.Key,
                // The inner GroupBy is not redundant. Averaging across regions is the
                // repository's job, so one crop should appear once per month — but a plain
                // ToDictionary *throws* on a duplicate key, and turning a data anomaly into a
                // 500 on the whole price chart is a worse trade than quietly taking the last
                // row. Last wins.
                g => g.GroupBy(r => r.CropName, StringComparer.Ordinal)
                      .ToDictionary(c => c.Key, c => c.Last().AveragePricePerKg, StringComparer.Ordinal));

        var points = new List<PricePointDto>(months);

        for (var offset = 0; offset < months; offset++)
        {
            var month = firstMonth.AddMonths(offset);
            pricesByMonth.TryGetValue(month, out var observed);

            var prices = new Dictionary<string, decimal>(cropNames.Count, StringComparer.Ordinal);

            foreach (var crop in cropNames)
            {
                prices[crop] = observed is not null && observed.TryGetValue(crop, out var price)
                    ? price
                    : MissingPrice;
            }

            points.Add(new PricePointDto(month.ToIsoDate(), prices));
        }

        // Ascending by construction rather than by a trailing OrderBy: the loop walks the axis
        // forward, so there is no ordering to get wrong and none to re-establish. Ordering is
        // part of the contract — a line chart handed unordered points draws a scribble.
        return new PriceTrendsDto(points);
    }

    private static DateOnly DateOnlyFromClock(DateTime instant) =>
        new(instant.Year, instant.Month, 1);
}
