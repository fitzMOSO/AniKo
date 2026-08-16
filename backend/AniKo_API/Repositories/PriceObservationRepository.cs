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
                g.Average(p => p.PricePerKg)))
            .ToListAsync(cancellationToken);
    }
}
