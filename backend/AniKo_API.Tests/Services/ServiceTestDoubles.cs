using AniKo_API.Models;
using AniKo_API.Repositories;

namespace AniKo_API.Tests.Services;

/// <summary>
/// Hand-written stand-ins for the repository interfaces and the clock.
/// <para>
/// No mocking library, and that is a decision rather than an omission. Every one of these
/// interfaces has one or two methods returning a canned list; a mocking framework would replace
/// twenty lines of obvious C# with a setup DSL, a package, and a stack trace that names the proxy
/// instead of the test. It would also make the *unused* members invisible, where here they throw
/// with a message that says which service reached for something it should not have.
/// </para>
/// </summary>
internal static class TestDoubleNotes;

/// <summary>
/// A clock frozen at a chosen instant.
/// </summary>
/// <remarks>
/// <c>Microsoft.Extensions.Time.Testing.FakeTimeProvider</c> is the library answer and would be
/// preferable, but it is not referenced by this test project and the csproj is not this change's
/// to edit. <see cref="TimeProvider"/> exists precisely so that this substitution costs three
/// lines.
/// </remarks>
internal sealed class FrozenTimeProvider(DateTimeOffset now) : TimeProvider
{
    /// <summary>Convenience for the common case of a UTC instant expressed as a DateTime.</summary>
    public FrozenTimeProvider(DateTime utcNow)
        : this(new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)))
    {
    }

    public override DateTimeOffset GetUtcNow() => now;
}

/// <summary>
/// The members of <see cref="IRepository{T}"/> that no dashboard service uses.
/// </summary>
/// <remarks>
/// Throwing rather than returning empty. An empty list here would let a service quietly start
/// depending on <c>ListAsync</c> — loading whole entity graphs instead of the projected query
/// shapes the repositories exist to provide — and every test would keep passing.
/// </remarks>
internal abstract class UnusedRepositoryMembers<T> : IRepository<T>
    where T : class
{
    public Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException($"No dashboard service should call ListAsync<{typeof(T).Name}>.");

    public Task<T?> FindAsync(int id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException($"No dashboard service should call FindAsync<{typeof(T).Name}>.");
}

internal sealed class FakeOrderRepository : UnusedRepositoryMembers<Order>, IOrderRepository
{
    public IReadOnlyList<OrderStatsRow> StatsRows { get; init; } = [];

    public IReadOnlyList<RecentOrderRow> RecentRows { get; init; } = [];

    /// <summary>The arguments the service actually passed, so a test can assert pass-through.</summary>
    public DateTime? LastSince { get; private set; }

    public int? LastRecentLimit { get; private set; }

    public Task<IReadOnlyList<OrderStatsRow>> ListSinceAsync(
        DateTime since,
        CancellationToken cancellationToken = default)
    {
        LastSince = since;

        // Filtered here rather than returned wholesale, because that is what the real repository
        // does. A fake that ignores `since` would let a service that forgot to split its windows
        // still pass.
        IReadOnlyList<OrderStatsRow> rows = [.. StatsRows.Where(r => r.CreatedAt >= since)];
        return Task.FromResult(rows);
    }

    public Task<IReadOnlyList<RecentOrderRow>> ListRecentAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        LastRecentLimit = limit;

        IReadOnlyList<RecentOrderRow> rows = [.. RecentRows.Take(limit)];
        return Task.FromResult(rows);
    }
}

internal sealed class FakeListingRepository : UnusedRepositoryMembers<Listing>, IListingRepository
{
    public IReadOnlyList<FeaturedListingRow> Rows { get; init; } = [];

    public int? LastLimit { get; private set; }

    public Task<IReadOnlyList<FeaturedListingRow>> ListFeaturedAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        LastLimit = limit;

        IReadOnlyList<FeaturedListingRow> rows = [.. Rows.Take(limit)];
        return Task.FromResult(rows);
    }
}

internal sealed class FakeSupplierRepository : UnusedRepositoryMembers<Supplier>, ISupplierRepository
{
    public IReadOnlyList<SupplierWithCrops> Rows { get; init; } = [];

    public Task<IReadOnlyList<SupplierWithCrops>> ListVerifiedWithCropsAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Rows);
}

internal sealed class FakePriceObservationRepository
    : UnusedRepositoryMembers<PriceObservation>, IPriceObservationRepository
{
    public IReadOnlyList<MonthlyCropPrice> Rows { get; init; } = [];

    public DateOnly? LastFirstMonth { get; private set; }

    public Task<IReadOnlyList<MonthlyCropPrice>> ListMonthlyAveragesAsync(
        DateOnly firstMonth,
        CancellationToken cancellationToken = default)
    {
        LastFirstMonth = firstMonth;

        IReadOnlyList<MonthlyCropPrice> rows =
            [.. Rows.Where(r => r.Month >= firstMonth).OrderBy(r => r.Month)];

        return Task.FromResult(rows);
    }
}

internal sealed class FakeCropRepository : UnusedRepositoryMembers<Crop>, ICropRepository
{
    public IReadOnlyList<string> Names { get; init; } = [];

    public Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Names);
}

/// <summary>Row builders, so a test body reads as the case it is exercising and nothing else.</summary>
internal static class Rows
{
    public static OrderStatsRow Order(
        DateTime createdAt,
        int supplierId = 1,
        int quantityKg = 100,
        decimal pricePerKg = 50m,
        OrderStatus status = OrderStatus.Confirmed) =>
        new(supplierId, quantityKg, pricePerKg, status, createdAt);

    public static RecentOrderRow Recent(
        string reference,
        DateTime createdAt,
        OrderStatus status = OrderStatus.Confirmed) =>
        new(
            Reference: reference,
            ListingName: $"Lot for {reference}",
            SupplierName: "Bataan Rice Growers",
            QuantityKg: 1_500,
            Status: status,
            EstimatedDelivery: DateOnly.FromDateTime(createdAt.AddDays(14)),
            CreatedAt: createdAt);

    public static FeaturedListingRow Featured(int id, string name) =>
        new(
            Id: id,
            Name: name,
            CropName: "rice",
            Grade: "A",
            SupplierName: "Bataan Rice Growers",
            Region: "Balanga, Bataan",
            Verified: true,
            VolumeKg: 24_000,
            MinimumOrderKg: 500,
            PricePerKg: 58.50m);

    public static SupplierWithCrops VerifiedSupplier(
        int id,
        string name,
        double latitude,
        double longitude,
        params string[] crops) =>
        new(
            new Supplier
            {
                Id = id,
                Name = name,
                Region = "Balanga, Bataan",
                Latitude = latitude,
                Longitude = longitude,
                Verified = true,
            },
            crops);
}
