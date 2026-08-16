using AniKo_API.Models;

namespace AniKo_API.Repositories;

// ---------------------------------------------------------------------------
// Query-shape records
// ---------------------------------------------------------------------------
// These are neither entities nor DTOs, and the distinction is the point of the file. An entity is
// what the database holds; a DTO is what the browser receives. These sit between: they are the
// shape a specific query returns, so a repository can project in SQL rather than hand a service a
// graph of entities and hope it only touches the loaded parts. Returning entities with unloaded
// navigation properties is how a service ends up issuing N queries from inside a loop with
// nothing in the code looking wrong.

/// <summary>
/// One order flattened for the stats calculation: enough to count it, price it, and attribute it
/// to a supplier, and nothing else.
/// </summary>
/// <remarks>
/// <see cref="PricePerKg"/> is copied from the listing at read time rather than stored on the
/// order. That makes spend a *current-price* figure, not a historical one — if a listing's price
/// changes, past spend changes with it. For a demo dashboard over seeded data that is invisible,
/// and it is recorded here so that the day it stops being acceptable, the fix is known: the price
/// has to be captured onto the order at purchase time, which is a schema change.
/// </remarks>
/// <param name="SupplierId">The supplier behind the ordered listing, for the distinct-supplier tile.</param>
/// <param name="QuantityKg">Kilogrammes ordered.</param>
/// <param name="PricePerKg">The listing's current price — see the remark above.</param>
/// <param name="Status">Used only to decide whether the order counts as active.</param>
/// <param name="CreatedAt">
/// When the order was placed, which is what both windows are cut on.
/// Must be <see cref="DateTimeKind.Utc"/>. This is a real constraint the type cannot express:
/// Npgsql throws when an <c>Unspecified</c> <see cref="DateTime"/> is compared against a
/// <c>timestamptz</c> column, so an ambient-kind value fails at the database rather than at the
/// call site. A <see cref="DateTimeOffset"/> would say so in the signature; it is not used here
/// only because every producer of this value is already UTC by construction.
/// </param>
public record OrderStatsRow(
    int SupplierId,
    int QuantityKg,
    decimal PricePerKg,
    OrderStatus Status,
    DateTime CreatedAt);

/// <summary>
/// A supplier together with the crops it actually lists.
/// </summary>
/// <remarks>
/// <para>
/// The crop list has no column behind it. The frontend's <c>Supplier.crops</c> is derived by
/// walking supplier → listings → crop and taking distinct names, which is a join the repository
/// does once for the whole page rather than once per supplier.
/// </para>
/// <para>
/// <b>This record carries an entity, which is the exception to the rule stated at the top of this
/// file, so here is the argument for it.</b> The rule exists to stop a service being handed a
/// graph it can lazily walk into N+1 queries. <see cref="Supplier"/> has no collection navigation
/// to walk — its only reference is <c>AppUser</c>, which nothing downstream touches — so the
/// hazard the rule guards against is absent. The alternative, a flat record, would restate all
/// six of the supplier's scalar fields verbatim and then need updating in lockstep with the
/// entity forever. Duplicating a type to satisfy a rule whose reason does not apply is worse than
/// the inconsistency. If <c>Supplier</c> ever gains a collection navigation, this becomes wrong
/// and should be flattened.
/// </para>
/// </remarks>
/// <param name="Supplier">The supplier entity, carried whole — see the exception argued above.</param>
/// <param name="Crops">Distinct lowercase crop names, sorted, so the rendered chip order is
/// stable between calls. Empty is legitimate — a supplier with no listings.</param>
public record SupplierWithCrops(Supplier Supplier, IReadOnlyList<string> Crops);

/// <summary>A listing with the two names its card renders, resolved in SQL.</summary>
public record FeaturedListingRow(
    int Id,
    string Name,
    string CropName,
    string Grade,
    string SupplierName,
    string Region,
    bool Verified,
    int VolumeKg,
    int MinimumOrderKg,
    decimal PricePerKg);

/// <summary>
/// An order with the listing and supplier names the table renders.
/// </summary>
/// <remarks>
/// There is deliberately no surrogate <c>Id</c> here. The wire contract's <c>id</c> is
/// <see cref="Reference"/> — the quotable "AK-1003" — so carrying the primary key as well would
/// offer the mapper a choice it must never make.
/// </remarks>
public record RecentOrderRow(
    string Reference,
    string ListingName,
    string SupplierName,
    int QuantityKg,
    OrderStatus Status,
    DateOnly EstimatedDelivery,
    DateTime CreatedAt);

/// <summary>One crop's price in one month, already averaged across regions.</summary>
/// <remarks>
/// The table holds one row per crop *per region* per month, but the chart draws one line per
/// crop. Averaging across regions is the aggregation that reconciles the two, and it happens in
/// SQL because doing it in memory means transferring every region's row in order to discard it.
/// </remarks>
public record MonthlyCropPrice(DateOnly Month, string CropName, decimal AveragePricePerKg);
