namespace AniKo_API.Models;

/// <summary>
/// The four states an order can be in. Exactly four, because the frontend has badge colours
/// for exactly these four (<c>STATUS</c> in <c>lib/chart-theme.ts</c>, and <c>StatusKey</c>
/// types <c>features/orders/types.ts</c> against it). Adding a fifth without a matching badge
/// colour would render as an unstyled badge at runtime rather than failing anywhere visible,
/// so a fifth is a coordinated frontend-and-backend change.
/// </summary>
public enum OrderStatus
{
    Confirmed,
    Processing,
    Shipped,
    Delivered,
}

/// <summary>
/// A buyer's purchase against a listing. Feeds Recent Orders.
/// </summary>
public class Order
{
    public int Id { get; set; }

    /// <summary>
    /// The human reference shown verbatim in the table, e.g. "ORD-2418". Separate from
    /// <see cref="Id"/> so the surrogate key stays an implementation detail and the reference
    /// stays quotable in a support conversation. Uniquely indexed: two orders sharing a
    /// reference makes that conversation impossible.
    /// </summary>
    public required string Reference { get; set; }

    public int BuyerId { get; set; }

    public AppUser? Buyer { get; set; }

    public int ListingId { get; set; }

    public Listing? Listing { get; set; }

    /// <summary>Kilogrammes. The view owns the locale formatting.</summary>
    public int QuantityKg { get; set; }

    public OrderStatus Status { get; set; }

    /// <summary>
    /// A calendar date, not an instant — <c>date</c>, not <c>timestamptz</c>. The frontend
    /// types it as ISO <c>YYYY-MM-DD</c> precisely because nobody promises an hour of arrival,
    /// and storing it as a timestamp would invent a midnight that then shifts by timezone.
    /// </summary>
    public DateOnly EstimatedDelivery { get; set; }

    /// <summary>
    /// UTC. This is what "recent" means — when the order was placed, not when it arrives —
    /// so it is the sort key for <c>GET /api/v1/orders/recent</c> and is indexed.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
