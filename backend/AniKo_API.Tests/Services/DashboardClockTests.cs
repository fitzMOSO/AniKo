using AniKo_API.Services;

namespace AniKo_API.Tests.Services;

/// <summary>
/// The clock exists because every dashboard window was anchored to the wall clock while the
/// seeded data is anchored to a fixed epoch. The two drift apart, and around 2026-09-06 the
/// 30-day order window slides past the newest seeded order and three tiles report zero at once.
/// </summary>
public class DashboardClockTests
{
    private static readonly DateTime WallClock = new(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc);

    private static DashboardClock Build(DateTime? latestOrder, out FakeOrderRepository repository)
    {
        repository = new FakeOrderRepository { LatestCreatedAt = latestOrder };
        var timeProvider = new FrozenTimeProvider(WallClock);
        return new DashboardClock(repository, new DashboardClockCache(timeProvider), timeProvider);
    }

    [Fact]
    public async Task ReferenceNow_LatestActivityBehindTheWallClock_UsesTheActivity()
    {
        var latest = new DateTime(2026, 7, 31, 3, 0, 0, DateTimeKind.Utc);
        var clock = Build(latest, out _);

        Assert.Equal(latest, await clock.ReferenceNowAsync());
    }

    [Fact]
    public async Task ReferenceNow_LatestActivityAheadOfTheWallClock_StillUsesTheActivity()
    {
        // A future-dated row is not rejected anywhere (see IOrderRepository.ListSinceAsync), so
        // "the later of the two" and "the activity" differ here. The activity wins either way:
        // a window that excluded the newest order would be a window with a hole at its top.
        var latest = new DateTime(2027, 1, 4, 0, 0, 0, DateTimeKind.Utc);
        var clock = Build(latest, out _);

        Assert.Equal(latest, await clock.ReferenceNowAsync());
    }

    [Fact]
    public async Task ReferenceNow_EmptyDatabase_FallsBackToTheWallClock()
    {
        var clock = Build(null, out _);

        Assert.Equal(WallClock, await clock.ReferenceNowAsync());
    }

    [Fact]
    public async Task ReferenceNow_RepeatedCalls_QueryOnceInsideTheCacheWindow()
    {
        var latest = new DateTime(2026, 7, 31, 3, 0, 0, DateTimeKind.Utc);
        var clock = Build(latest, out var repository);

        await clock.ReferenceNowAsync();
        await clock.ReferenceNowAsync();
        await clock.ReferenceNowAsync();

        // Five panels load on one page view, each in its own request scope. Without a shared
        // cache that is five identical MAX(created_at) queries per visit.
        Assert.Equal(1, repository.LatestCreatedAtCalls);
    }
}
