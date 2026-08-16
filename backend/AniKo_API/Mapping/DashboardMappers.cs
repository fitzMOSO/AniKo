using System.Globalization;
using AniKo_API.Dtos;
using AniKo_API.Models;
using AniKo_API.Repositories;

namespace AniKo_API.Mapping;

/// <summary>
/// Row-to-DTO translation for the five dashboard endpoints.
/// <para>
/// Static and dependency-free on purpose. Every method here is a pure function of its arguments,
/// which is what makes the conversions below assertable in a unit test with no database, no host
/// and no fixture — and these are precisely the conversions that fail without throwing.
/// </para>
/// <para>
/// <b>The rule this file exists to enforce:</b> nothing outside it converts a <i>row</i> into its
/// DTO. Not the services, not the endpoints. The moment a second place builds a
/// <see cref="RecentOrderDto"/>, the lowercase-status rule below has two homes and one of them
/// will be wrong.
/// </para>
/// <para>
/// The rule stops at row level, and the boundary is worth stating because an earlier draft of
/// this comment claimed the stronger "nothing outside constructs a dashboard DTO" — which was
/// not true of the code and could not be made true without moving logic here that does not
/// belong. The services still construct the five envelopes
/// (<see cref="RecentOrdersDto"/> and friends) and the two DTOs that are computed rather than
/// translated: <see cref="OverviewStatDto"/> is a rounded arithmetic result, and
/// <see cref="PricePointDto"/> is the output of a pivot that needs the full crop list. Neither is
/// a translation of a row, so neither has a mapper. The distinction that matters is not "who
/// calls <c>new</c>" but "where does a field change meaning" — and that only happens here.
/// </para>
/// </summary>
public static class DashboardMappers
{
    /// <summary>
    /// The wire format for every date the frontend types as a <c>string</c>.
    /// </summary>
    /// <remarks>
    /// Pinned to <see cref="CultureInfo.InvariantCulture"/> rather than left to the ambient
    /// culture, because <c>DateOnly.ToString("yyyy-MM-dd")</c> is not culture-proof.
    /// <para>
    /// It is worth being precise about which part is dangerous, because the obvious guess is
    /// wrong. The separator is <i>not</i> the problem: in a .NET custom format string <c>/</c> is
    /// the placeholder that a culture substitutes, while <c>-</c> is a literal. A culture whose
    /// date separator is <c>.</c> or <c>/</c> still emits <c>2026-08-01</c> here.
    /// </para>
    /// <para>
    /// The problem is the <b>calendar</b>. <c>yyyy</c> renders the year in the current culture's
    /// own calendar, so the unpinned call produces <c>2569-08-01</c> under <c>th-TH</c> (Buddhist
    /// era) and <c>1448-02-18</c> under <c>ar-SA</c> (Umm al-Qura — a different day and month, not
    /// merely a different year). Both are well-formed <c>yyyy-MM-dd</c> strings. Nothing rejects
    /// them; the chart simply plots the wrong dates, or the orders table shows a delivery 543
    /// years out, on a server whose only sin was a locale set at deploy time.
    /// </para>
    /// <para>
    /// <c>DashboardMappersTests</c> pins this by asserting the <i>unpinned</i> call really does
    /// return <c>2569-08-01</c> under <c>th-TH</c>, so this argument cannot rot into folklore and
    /// the pin cannot be deleted without a test going red.
    /// </para>
    /// </remarks>
    private const string IsoDateFormat = "yyyy-MM-dd";

    internal static string ToIsoDate(this DateOnly date) =>
        date.ToString(IsoDateFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// Converts an <see cref="OrderStatus"/> to the frontend's <c>StatusKey</c>.
    /// </summary>
    /// <remarks>
    /// <b>This is the single most dangerous line in the backend, and it is one line.</b>
    /// <para>
    /// The enum is persisted by name, so the database holds <c>"Confirmed"</c>. The frontend's
    /// <c>StatusKey</c> is <c>"confirmed"</c>, and it is used as a key into two lookup tables:
    /// the <c>STATUS</c> colour map in <c>lib/chart-theme.ts</c> and the <c>orders.status.*</c>
    /// i18n bundle. Neither lookup throws on a miss. Emitting <c>"Confirmed"</c> produces a badge
    /// with no colour and a label that renders the raw key, on a page that otherwise loads
    /// perfectly, with a 200 in the network tab and nothing in any log on either side.
    /// </para>
    /// <para>
    /// <see cref="CultureInfo.InvariantCulture"/> again, and for a sharper reason than the date
    /// format. Under a Turkish culture, <c>"I".ToLower()</c> is <c>"ı"</c> — a dotless i.
    /// <c>Processing</c>, <c>Shipped</c> and <c>Delivered</c> contain no capital I, so this would
    /// pass every test and every review, and break only for a deployment whose culture happened
    /// to be tr-TR, and only if a status name ever gained one. Invariant removes the question.
    /// </para>
    /// </remarks>
    public static string ToStatusKey(this OrderStatus status) =>
        status.ToString().ToLowerInvariant();

    /// <summary>Projects a supplier's stored coordinates onto the wire's <c>LatLng</c>.</summary>
    /// <remarks>
    /// Named <c>ToLatLng</c> rather than joining the <c>ToDto</c> overload set, which is not a
    /// style preference. Four <c>ToDto</c> overloads make the bare method group
    /// <c>DashboardMappers.ToDto</c> ambiguous: <c>rows.Select(DashboardMappers.ToDto)</c> stops
    /// compiling and has to be written as a lambda, and a <c>&lt;see cref="ToDto"/&gt;</c> raises
    /// CS0419. The other three take a distinct row type each and read naturally as "to the DTO";
    /// this one returns a fragment of a DTO, so it was the odd one out anyway.
    /// </remarks>
    public static LatLngDto ToLatLng(this Supplier supplier) =>
        new(supplier.Latitude, supplier.Longitude);

    public static NearbySupplierDto ToDto(this SupplierWithCrops row, double distanceKm) =>
        new(
            // The frontend types every id as a string. Invariant culture for the same reason as
            // above: a culture with a digit-grouping default would not affect "R", but nothing
            // here benefits from finding out.
            Id: row.Supplier.Id.ToString(CultureInfo.InvariantCulture),
            Name: row.Supplier.Name,
            Region: row.Supplier.Region,
            Location: row.Supplier.ToLatLng(),
            Verified: row.Supplier.Verified,
            Crops: row.Crops,
            DistanceKm: distanceKm);

    public static FeaturedLotDto ToDto(this FeaturedListingRow row) =>
        new(
            Id: row.Id.ToString(CultureInfo.InvariantCulture),
            Name: row.Name,
            Crop: row.CropName,
            Grade: row.Grade,
            Supplier: row.SupplierName,
            Region: row.Region,
            Verified: row.Verified,
            VolumeKg: row.VolumeKg,
            // Note the rename: the entity says MinimumOrderKg, the wire says minOrderKg, because
            // the frontend's Lot type says minOrderKg. The contract is not ours to tidy.
            MinOrderKg: row.MinimumOrderKg,
            PricePerKg: row.PricePerKg);

    public static RecentOrderDto ToDto(this RecentOrderRow row) =>
        new(
            // The human reference, not the surrogate key. The frontend renders `id` directly in
            // the table's first column, and a buyer quoting "7" to support helps nobody, whereas
            // "AK-1003" is the whole reason Reference exists.
            Id: row.Reference,
            Product: row.ListingName,
            Supplier: row.SupplierName,
            QuantityKg: row.QuantityKg,
            Status: row.Status.ToStatusKey(),
            EstimatedDelivery: row.EstimatedDelivery.ToIsoDate());
}
