using AniKo_API.Models;

namespace AniKo_API.Repositories;

public interface IOrderRepository : IRepository<Order>
{
    /// <summary>
    /// The most recently *placed* orders, newest first.
    /// </summary>
    /// <param name="limit">Already validated and in range. A repository is the wrong place to
    /// discover that a caller asked for 10,000 rows — see the validators.</param>
    /// <param name="cancellationToken">Aborts the query when the request is abandoned.</param>
    Task<IReadOnlyList<RecentOrderRow>> ListRecentAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every order placed at or after <paramref name="since"/>, flattened for stats.
    /// </summary>
    /// <remarks>
    /// One call covers both the current window and the comparison window: the service is handed
    /// the union and splits it. Two calls would double the round trips to answer a question about
    /// one contiguous range, and would also open a gap in which an order placed between them
    /// counts twice or not at all.
    /// <para>
    /// There is no upper bound, so a row with a future <c>CreatedAt</c> lands in the current
    /// window. Nothing rejects one; the seeded data never produces one.
    /// </para>
    /// </remarks>
    /// <param name="since">Inclusive lower bound. Must be <see cref="DateTimeKind.Utc"/> — see
    /// <see cref="OrderStatsRow"/>.</param>
    /// <param name="cancellationToken">Aborts the query when the request is abandoned.</param>
    Task<IReadOnlyList<OrderStatsRow>> ListSinceAsync(DateTime since, CancellationToken cancellationToken = default);

    /// <summary>
    /// When the most recent order was placed, or <c>null</c> if there are none.
    /// </summary>
    /// <remarks>
    /// This is what <see cref="AniKo_API.Services.IDashboardClock"/> anchors its windows on. A
    /// single <c>MAX(created_at)</c> rather than <c>ListRecentAsync(1)</c>: the latter projects a
    /// row through two joins to read one timestamp off it.
    /// </remarks>
    Task<DateTime?> LatestCreatedAtAsync(CancellationToken cancellationToken = default);
}
