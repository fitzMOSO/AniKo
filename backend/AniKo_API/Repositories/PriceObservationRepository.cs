using AniKo_API.Data;
using AniKo_API.Models;
using Microsoft.EntityFrameworkCore;

namespace AniKo_API.Repositories;

/// <inheritdoc cref="IPriceObservationRepository"/>
public sealed class PriceObservationRepository : Repository<PriceObservation>, IPriceObservationRepository
{
    public PriceObservationRepository(AniKoDbContext db)
        : base(db)
    {
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MonthlyCropPrice>> ListMonthlyAveragesAsync(
        DateOnly firstMonth,
        CancellationToken cancellationToken = default)
    {
        // The whole method is one GROUP BY, and the reason it is written as a group rather than
        // as a fetch-then-aggregate is size: the table holds one row per crop per region per
        // month, and the chart wants one point per crop per month. Averaging in memory means
        // transferring every region's row across the wire in order to throw it away, and the
        // ratio is the number of regions — invisible at demo scale, linear in the thing most
        // likely to grow.
        //
        // Ordering is applied to the grouping key, before the projection: EF translates
        // `OrderBy(g => g.Key.Month)` to the grouped column, whereas ordering the projected
        // record afterwards asks it to sort by a constructor argument it has to see through.
        // Crop name is the tiebreaker so the row order is total rather than merely month-correct,
        // which is what makes an assertion on the whole sequence possible.
        return await Query()
            .Where(p => p.Month >= firstMonth)
            .GroupBy(p => new { p.Month, CropName = p.Crop!.Name })
            .OrderBy(g => g.Key.Month)
            .ThenBy(g => g.Key.CropName)
            .Select(g => new MonthlyCropPrice(
                g.Key.Month,
                g.Key.CropName,

                // AVG over numeric(18,2) in Postgres returns numeric, not double precision, so
                // this is exact-decimal arithmetic end to end. The same expression on a
                // float-typed column would return a binary float and the chart's y-axis would
                // pick up rounding noise that no test would ever pin down.
                //
                // Rounded to 2dp because AVG(numeric) keeps extra scale to stay exact: averaging
                // 26.20 and 26.20 yields `26.2000000000000000`, which is the correct number and a
                // terrible thing to put on the wire. JSON.parse reads it back as 26.2 so nothing
                // breaks, but it roughly triples the size of the largest response for digits that
                // carry no information — and these are pesos per kilo, where the second decimal
                // place is already the smallest unit that exists.
                //
                // Rounded in SQL, not in memory, so the projection stays a projection.
                Math.Round(g.Average(p => p.PricePerKg), 2)))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<DateOnly?> LatestMonthAsync(CancellationToken cancellationToken = default)
    {
        // Nullable projection before Max: MaxAsync over a non-nullable DateOnly throws on an
        // empty table rather than yielding a null, and an empty price table is a legitimate
        // state (a fresh database before the seeder runs), not an error.
        return await Query().Select(o => (DateOnly?)o.Month).MaxAsync(cancellationToken);
    }
}
