namespace AniKo_API.Dtos;

/// <summary>
/// The wire contract for the five dashboard endpoints.
/// <para>
/// These shapes are not invented here — they are transcribed from the frontend's own
/// <c>features/*/types.ts</c>, which already exist and already have tests written against them.
/// The backend is the later half of a contract that is already in use, so where the two could
/// differ, this file follows the frontend.
/// </para>
/// <para>
/// <b>Three transcription hazards are worth stating once, here, because every one of them fails
/// silently rather than loudly:</b>
/// </para>
/// <list type="number">
/// <item>
/// <b>Casing.</b> The frontend's <c>SeriesKey</c> and <c>StatusKey</c> are lowercase literals
/// (<c>rice</c>, <c>confirmed</c>) used as lookup keys into <c>chart-theme.ts</c> and into the
/// i18n bundle. The database stores <c>Order.Status</c> as <c>"Confirmed"</c>, because the enum
/// is persisted by name. Emitting that verbatim produces a badge with no colour and a label with
/// no translation, and throws nothing anywhere. Mapping to lowercase is mandatory, not cosmetic.
/// </item>
/// <item>
/// <b>Ids are strings.</b> The frontend types every <c>id</c> as <c>string</c>; the database uses
/// <c>int</c>. JSON would happily carry a number and the frontend would happily accept it, right
/// up to the first <c>===</c> comparison or map key.
/// </item>
/// <item>
/// <b>Dates are strings.</b> <c>PricePoint.date</c> and <c>Order.estimatedDelivery</c> are
/// strings on the wire. Serialising a <c>DateOnly</c> gives <c>"2026-08-01"</c>, which is what is
/// wanted; serialising a <c>DateTime</c> would append a time and a zone nobody asked for.
/// </item>
/// </list>
/// </summary>
internal static class DtoContractNotes;

// ---------------------------------------------------------------------------
// GET /api/v1/buyer/overview/stats
// ---------------------------------------------------------------------------

/// <summary>
/// One stat tile's figures.
/// </summary>
/// <remarks>
/// Deliberately narrower than the frontend's <c>OverviewStat</c>. That interface also carries
/// <c>labelKey</c>, <c>icon</c> and <c>upIsGood</c> — a translation key, a Lucide component and a
/// presentation rule. None of those are facts about the data, and an icon cannot be serialised at
/// all. The frontend already owns them; sending them from here would create a second place to
/// change the wording of a label.
/// </remarks>
/// <param name="Key">Stable identifier the frontend joins on: <c>activeOrders</c>, <c>spend</c>,
/// <c>suppliers</c>, <c>avgPrice</c>.</param>
/// <param name="Value">The current-period figure.</param>
/// <param name="DeltaPercent">Change against the prior period, as a percentage. Negative means
/// down. Whether down is *bad* is the frontend's <c>upIsGood</c> rule, not ours.</param>
public record OverviewStatDto(string Key, decimal Value, decimal DeltaPercent);

/// <param name="Stats">Always all four tiles, in a stable order.</param>
public record OverviewStatsDto(IReadOnlyList<OverviewStatDto> Stats);

// ---------------------------------------------------------------------------
// GET /api/v1/pricing/trends?months=
// ---------------------------------------------------------------------------

/// <summary>
/// One point on the price chart: a date plus one price per crop.
/// </summary>
/// <remarks>
/// <b>Pivoted, not grouped.</b> The obvious server shape is one series per crop, each holding its
/// own points. The frontend's <c>PricePoint</c> is the transpose of that —
/// <c>{ date } &amp; Record&lt;SeriesKey, number&gt;</c>, one row per date carrying every crop —
/// because that is the row shape Recharts consumes directly. Emitting the grouped shape would
/// require the frontend to pivot on every render, so the pivot happens once, here.
/// <para>
/// <c>Prices</c> is a dictionary rather than three named properties so that adding a fourth crop
/// is reference data, not a schema change. The keys are crop names exactly as stored, which are
/// already lowercase and already match <c>SeriesKey</c>.
/// </para>
/// </remarks>
/// <param name="Date">ISO date, first day of the month, e.g. <c>2026-08-01</c>.</param>
/// <param name="Prices">Crop name to price per kg in PHP.</param>
public record PricePointDto(string Date, IReadOnlyDictionary<string, decimal> Prices);

/// <param name="Points">Ascending by date. Ordering is part of the contract: a line chart handed
/// unordered points draws a scribble rather than failing.</param>
public record PriceTrendsDto(IReadOnlyList<PricePointDto> Points);

// ---------------------------------------------------------------------------
// GET /api/v1/suppliers/nearby
// ---------------------------------------------------------------------------

/// <param name="Lat">Degrees north.</param>
/// <param name="Lng">Degrees east. Named <c>lng</c>, not <c>lon</c> or <c>long</c>, to match the
/// frontend's <c>LatLng</c> — a mismatch here deserialises to 0 and puts every pin off Africa.</param>
public record LatLngDto(double Lat, double Lng);

/// <summary>A verified supplier with its distance from the query origin.</summary>
/// <remarks>
/// <c>Crops</c> has no direct source in the schema: a supplier has listings, and listings have
/// crops. It is derived by walking that relation and taking the distinct crop names. The frontend
/// uses it to render crop chips, so an empty list is legitimate (a supplier with no listings) and
/// must not be confused with a mapping failure.
/// </remarks>
public record NearbySupplierDto(
    string Id,
    string Name,
    string Region,
    LatLngDto Location,
    bool Verified,
    IReadOnlyList<string> Crops,
    double DistanceKm);

/// <param name="Origin">Echoed back deliberately. The frontend renders "within N km of X" and
/// centres the map on it; deriving it a second time on the client is how the list and the map
/// come to disagree.</param>
/// <param name="Suppliers">Ascending by distance.</param>
public record NearbySuppliersDto(LatLngDto Origin, IReadOnlyList<NearbySupplierDto> Suppliers);

// ---------------------------------------------------------------------------
// GET /api/v1/listings/featured
// ---------------------------------------------------------------------------

/// <summary>One card in Featured Wholesale Lots.</summary>
/// <param name="Id">The listing's surrogate key as a string — the frontend types every id that way.</param>
/// <param name="Name">The lot's trade name, e.g. "Premium White Rice". Not the crop.</param>
/// <param name="Crop">Lowercase crop name, doubling as the frontend's <c>SeriesKey</c>.</param>
/// <param name="Grade">Trade grade as printed on the sack — "A", "B". A string, not a score.</param>
/// <param name="Supplier">The supplier's display name, flattened. The card shows a name, not an
/// object, and nesting one here would invite the frontend to start reaching into it.</param>
/// <param name="Region">Pre-composed "Municipality, Province".</param>
/// <param name="Verified">Whether the supplier behind this lot is verified, as captured on the lot.</param>
/// <param name="VolumeKg">Total volume on offer.</param>
/// <param name="MinOrderKg">Smallest order accepted. Note the name: the entity says
/// <c>MinimumOrderKg</c> and the wire says <c>minOrderKg</c>, because the frontend's <c>Lot</c>
/// does. The rename is the mapper's job and is asserted there.</param>
/// <param name="PricePerKg">PHP per kilogramme.</param>
public record FeaturedLotDto(
    string Id,
    string Name,
    string Crop,
    string Grade,
    string Supplier,
    string Region,
    bool Verified,
    int VolumeKg,
    int MinOrderKg,
    decimal PricePerKg);

public record FeaturedLotsDto(IReadOnlyList<FeaturedLotDto> Lots);

// ---------------------------------------------------------------------------
// GET /api/v1/orders/recent?limit=
// ---------------------------------------------------------------------------

/// <summary>One row in Recent Orders.</summary>
/// <param name="Id">The human reference — "AK-1003" — not the surrogate key. The table renders
/// this directly, and a buyer quoting "7" to support helps nobody.</param>
/// <param name="Product">The listing's name. Called <c>product</c> on the wire because that is
/// what the frontend's <c>Order</c> calls it.</param>
/// <param name="Supplier">The supplier's display name.</param>
/// <param name="QuantityKg">Kilogrammes ordered. The view owns the locale formatting.</param>
/// <param name="Status">**Lowercase.** See the casing note at the top of this file — this is the
/// single field most likely to be emitted wrong, and it fails without an error.</param>
/// <param name="EstimatedDelivery">ISO date, <c>yyyy-MM-dd</c>.</param>
public record RecentOrderDto(
    string Id,
    string Product,
    string Supplier,
    int QuantityKg,
    string Status,
    string EstimatedDelivery);

/// <param name="Orders">Descending by creation time — "recent" is about when the order was
/// placed, not when it is due to arrive.</param>
public record RecentOrdersDto(IReadOnlyList<RecentOrderDto> Orders);
