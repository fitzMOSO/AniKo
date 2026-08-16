namespace AniKo_API.Services;

/// <summary>
/// The instant that dashboard windows treat as "now".
/// </summary>
/// <remarks>
/// <para>
/// Not <see cref="TimeProvider"/>, and the difference is the whole point. A trailing 30-day
/// window read off the wall clock is correct only while data keeps arriving. Against a fixed
/// seed it drifts: the seeded orders span <c>SeedEpoch - 1..36 days</c> from 2026-08-01, so
/// around 2026-09-06 the window slides past the newest of them and the active-orders, spend and
/// distinct-supplier tiles all report zero on the same day. They do not fail — they truthfully
/// report a window containing nothing, which is why no test and no alert would have caught it.
/// </para>
/// <para>
/// The resolution is <c>latestActivity ?? wallClock</c>. This is not a demo affordance: with
/// real orders arriving, the latest activity <i>is</i> now and the behaviour is
/// indistinguishable from reading the clock. What it encodes is that a dashboard window is
/// relative to when things last happened, which is what such a window means. Anchoring on the
/// <c>SeedEpoch</c> constant was rejected for the opposite reason — it becomes wrong the day a
/// real row is written.
/// </para>
/// </remarks>
public interface IDashboardClock
{
    Task<DateTime> ReferenceNowAsync(CancellationToken cancellationToken = default);
}
