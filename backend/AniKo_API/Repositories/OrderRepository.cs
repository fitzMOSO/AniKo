using AniKo_API.Data;
using AniKo_API.Models;
using Microsoft.EntityFrameworkCore;

namespace AniKo_API.Repositories;

/// <inheritdoc cref="IOrderRepository"/>
public sealed class OrderRepository : Repository<Order>, IOrderRepository
{
    public OrderRepository(AniKoDbContext db)
        : base(db)
    {
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RecentOrderRow>> ListRecentAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        // Order before Take, obviously — but also Take before Select, which is less obvious and
        // is the reason the clauses are in this sequence rather than any other. Projecting first
        // would work and would produce the same rows; ordering the projection is what invites EF
        // to sort on a computed column instead of on orders.created_at, and the index behind that
        // sort is the only reason "recent" is not a full scan of the table.
        return await Query()
            .OrderByDescending(o => o.CreatedAt)
            .Take(limit)
            .Select(o => new RecentOrderRow(
                o.Reference,
                o.Listing!.Name,
                o.Listing.Supplier!.Name,
                o.QuantityKg,
                o.Status,
                o.EstimatedDelivery,
                o.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<OrderStatsRow>> ListSinceAsync(
        DateTime since,
        CancellationToken cancellationToken = default)
    {
        // `since` has to be UTC. Npgsql refuses a DateTime with Kind.Unspecified against a
        // `timestamp with time zone` parameter, so a caller that computed the window from
        // DateTime.Now would fail here at execution rather than quietly returning the wrong
        // eight hours of orders. That failure is the desirable outcome and it is left in place
        // rather than papered over with a SpecifyKind, which would guess.
        return await Query()
            .Where(o => o.CreatedAt >= since)

            // Newest first, matching ListRecentAsync. The interface does not specify an order
            // because the service aggregates rather than renders, but an unordered query is a
            // query whose result depends on the plan, and a stats window that changes row order
            // between runs is untestable for no gain.
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new OrderStatsRow(
                // Through the listing rather than off the order: an order has no supplier column,
                // and the supplier it is attributed to is whoever owns the lot it was placed
                // against.
                o.Listing!.SupplierId,
                o.QuantityKg,
                o.Listing.PricePerKg,
                o.Status,
                o.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
