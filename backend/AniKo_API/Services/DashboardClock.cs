using AniKo_API.Repositories;

namespace AniKo_API.Services;

/// <inheritdoc cref="IDashboardClock"/>
public sealed class DashboardClock(
    IOrderRepository orders,
    DashboardClockCache cache,
    TimeProvider timeProvider) : IDashboardClock
{
    public async Task<DateTime> ReferenceNowAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGet(out var cached))
        {
            return cached;
        }

        var latest = await orders.LatestCreatedAtAsync(cancellationToken).ConfigureAwait(false);

        // The resolved value is cached, not the nullable query result, so an empty database does
        // not re-query on every request just to fall through to the same clock reading.
        var resolved = latest ?? timeProvider.GetUtcNow().UtcDateTime;

        cache.Set(resolved);
        return resolved;
    }
}
