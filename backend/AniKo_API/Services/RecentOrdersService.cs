using AniKo_API.Dtos;
using AniKo_API.Mapping;
using AniKo_API.Repositories;

namespace AniKo_API.Services;

/// <summary>
/// The most recently placed orders, ready for the wire.
/// </summary>
/// <remarks>
/// The one transformation worth naming happens inside
/// <see cref="DashboardMappers.ToDto(RecentOrderRow)"/> and not here:
/// <c>OrderStatus.Confirmed</c> becomes <c>"confirmed"</c>. That lowercasing is the single
/// most failure-prone line in the backend and it has exactly one home, which is why this class
/// calls the mapper rather than building a <see cref="RecentOrderDto"/> itself.
/// <para>
/// Ordering is the repository's — "recent" means newest by <c>CreatedAt</c> descending, and
/// re-sorting here would create a second, silently divergent definition of the word.
/// </para>
/// </remarks>
public sealed class RecentOrdersService(IOrderRepository orders) : IRecentOrdersService
{
    public async Task<RecentOrdersDto> GetAsync(int limit, CancellationToken cancellationToken = default)
    {
        var rows = await orders.ListRecentAsync(limit, cancellationToken).ConfigureAwait(false);

        return new RecentOrdersDto([.. rows.Select(row => row.ToDto())]);
    }
}
