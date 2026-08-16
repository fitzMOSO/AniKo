namespace AniKo_API.Services;

/// <summary>
/// A 60-second in-process cache for the resolved reference instant.
/// </summary>
/// <remarks>
/// Singleton, while <see cref="DashboardClock"/> is scoped, and that split is the reason this
/// class exists at all. One page view issues five requests, each with its own DI scope and its
/// own <c>IOrderRepository</c>; a cache living on the clock would be a cache with a hit rate of
/// zero. Staleness costs nothing here: the value feeds 30-day and multi-month windows, so a
/// figure up to a minute old moves no boundary.
/// </remarks>
public sealed class DashboardClockCache(TimeProvider timeProvider)
{
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private readonly Lock _gate = new();
    private DateTime _value;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public bool TryGet(out DateTime value)
    {
        lock (_gate)
        {
            value = _value;
            return timeProvider.GetUtcNow() < _expiresAt;
        }
    }

    public void Set(DateTime value)
    {
        lock (_gate)
        {
            _value = value;
            _expiresAt = timeProvider.GetUtcNow() + Ttl;
        }
    }
}
