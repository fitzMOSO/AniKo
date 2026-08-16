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

    /// <summary>
    /// The most recent month that has any observation, or <c>null</c> when the table is empty.
    /// </summary>
    /// <remarks>
    /// This is the price series' own "now". It is deliberately NOT
    /// <see cref="AniKo_API.Services.IDashboardClock"/>: that resolves from orders, and orders and
    /// observations are seeded on different anchors, so borrowing one for the other reintroduces
    /// exactly the two-drifting-anchors defect this codebase has now fixed twice.
    /// </remarks>
    Task<DateOnly?> LatestMonthAsync(CancellationToken cancellationToken = default);
}
