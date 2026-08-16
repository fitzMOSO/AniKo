namespace AniKo_API.Dtos;

/// <summary>
/// The inbound half of the dashboard contract: the query parameters each endpoint accepts.
/// <para>
/// These are records rather than loose <c>int</c> parameters on an endpoint signature for one
/// reason — a record is a type, and a type can have a validator attached to it that is testable
/// without a request, a route, or a host. Phase E's exit criterion is exactly that: the range
/// rules below are asserted by unit tests that never open a socket.
/// </para>
/// <para>
/// <b>Every rule here rejects rather than clamps, and that is the whole design.</b> Clamping
/// <c>months=999</c> down to 24 produces a 200, a chart that renders, and a frontend bug that
/// nobody will ever find — the user sees two years of data and has no way to know they asked for
/// more. A 400 naming the parameter and its range puts the mistake in the caller's console on the
/// first run. The services document this expectation too (see <c>IDashboardServices</c>: "the
/// service clamps nothing"), so the validator is the only thing standing between a typo in a query
/// string and a silently wrong page.
/// </para>
/// </summary>
public static class DashboardRequestBounds
{
    /// <summary>Shortest price-trend window: a single month.</summary>
    public const int MinMonths = 1;

    /// <summary>
    /// Longest price-trend window. Two years, because that is the range selector's widest option
    /// and because the seeded price history does not go back further — asking for 120 months would
    /// return a chart padded with nothing and look like data loss.
    /// </summary>
    public const int MaxMonths = 24;

    /// <summary>
    /// The window used when <c>months</c> is omitted. Must sit inside
    /// [<see cref="MinMonths"/>, <see cref="MaxMonths"/>] — a default outside its own bounds turns
    /// every parameterless request into a 400, which is why there is a test for precisely that.
    /// </summary>
    public const int DefaultMonths = 6;

    /// <summary>A list of zero rows is a request for nothing; the caller meant to omit the call.</summary>
    public const int MinLimit = 1;

    /// <summary>
    /// Upper bound on any list endpoint. These are unpaginated panels on a dashboard, so the cap
    /// is a promise about response size rather than a performance guess.
    /// </summary>
    public const int MaxLimit = 50;

    /// <summary>The row count used when <c>limit</c> is omitted. Inside the bounds above.</summary>
    public const int DefaultLimit = 10;

    /// <summary>Degrees. Latitude is bounded by the poles.</summary>
    public const double MinLatitude = -90;

    /// <summary>Degrees. Inclusive: the pole itself is a real place.</summary>
    public const double MaxLatitude = 90;

    /// <summary>Degrees. The antimeridian, inclusive.</summary>
    public const double MinLongitude = -180;

    /// <summary>Degrees. The antimeridian from the other side, also inclusive.</summary>
    public const double MaxLongitude = 180;
}

/// <summary>
/// <c>GET /api/v1/pricing/trends?months=</c>
/// </summary>
/// <param name="Months">How many months of history to return, counting back from the current
/// month. Defaults to <see cref="DashboardRequestBounds.DefaultMonths"/> so that an omitted
/// parameter is a valid request rather than a 400.</param>
public record PriceTrendsRequest(int Months = DashboardRequestBounds.DefaultMonths);

/// <summary>
/// <c>GET /api/v1/suppliers/nearby?lat=&amp;lng=&amp;limit=</c>
/// </summary>
/// <remarks>
/// <b><see cref="Lat"/> and <see cref="Lng"/> deliberately have no default.</b> Every other
/// parameter in this file defaults, and it would be easy to give these one for symmetry — but the
/// only symmetric choice is <c>(0, 0)</c>, which is a perfectly valid coordinate in the Gulf of
/// Guinea. A missing origin would then validate, rank every Philippine supplier by its distance
/// from Null Island, and return a plausible-looking list in the wrong order. Leaving them required
/// makes an omitted origin a model-binding failure instead, which is loud.
/// </remarks>
/// <param name="Lat">Buyer latitude in decimal degrees.</param>
/// <param name="Lng">Buyer longitude in decimal degrees. <c>lng</c>, not <c>lon</c> — see
/// <see cref="LatLngDto"/>.</param>
/// <param name="Limit">Maximum suppliers to return, nearest first.</param>
public record NearbySuppliersRequest(
    double Lat,
    double Lng,
    int Limit = DashboardRequestBounds.DefaultLimit);

/// <summary>
/// <c>GET /api/v1/listings/featured?limit=</c>
/// </summary>
public record FeaturedLotsRequest(int Limit = DashboardRequestBounds.DefaultLimit);

/// <summary>
/// <c>GET /api/v1/orders/recent?limit=</c>
/// </summary>
public record RecentOrdersRequest(int Limit = DashboardRequestBounds.DefaultLimit);
