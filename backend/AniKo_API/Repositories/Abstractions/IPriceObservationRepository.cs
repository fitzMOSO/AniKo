using AniKo_API.Models;

namespace AniKo_API.Repositories;

public interface IPriceObservationRepository : IRepository<PriceObservation>
{
    /// <summary>
    /// Monthly average price per crop, from <paramref name="firstMonth"/> onward, ascending by
    /// month.
    /// </summary>
    /// <param name="firstMonth">Inclusive lower bound. Must be the first day of a month —
    /// <c>Month</c> is normalised that way on write, so a mid-month value would silently exclude
    /// the month it names rather than erroring.</param>
    /// <param name="cancellationToken">Aborts the query when the request is abandoned.</param>
    Task<IReadOnlyList<MonthlyCropPrice>> ListMonthlyAveragesAsync(
        DateOnly firstMonth,
        CancellationToken cancellationToken = default);
}
