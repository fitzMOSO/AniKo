using AniKo_API.Dtos;
using AniKo_API.Mapping;
using AniKo_API.Repositories;

namespace AniKo_API.Services;

/// <summary>
/// Ranks verified suppliers by great-circle distance from the buyer's position.
/// </summary>
/// <remarks>
/// The ranking happens here rather than in SQL, and <see cref="ISupplierRepository"/> explains
/// why: distance is a computed value with no column behind it, so Postgres cannot order by it
/// without PostGIS or an inline haversine. At demo scale — six suppliers — fetching all of them
/// and sorting in memory is both simpler and honest about the cost. The upgrade path when this
/// table reaches thousands of rows is a bounding-box pre-filter in SQL, not a cleverer service.
/// </remarks>
public sealed class NearbySupplierService(ISupplierRepository suppliers) : INearbySupplierService
{
    /// <summary>
    /// Decimal places on the emitted distance. The card renders "12.4 km away", and a raw double
    /// serialises as <c>12.438172091203941</c> — which is not more accurate, given that haversine
    /// over a sphere is good to a few hundred metres at these ranges, it is just longer.
    /// </summary>
    private const int DistanceDecimals = 1;

    public async Task<NearbySuppliersDto> GetAsync(
        LatLngDto origin,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(origin);

        var rows = await suppliers
            .ListVerifiedWithCropsAsync(cancellationToken)
            .ConfigureAwait(false);

        var ranked = rows
            .Select(row => new
            {
                Row = row,
                DistanceKm = GeoDistance.KilometresBetween(
                    origin.Lat,
                    origin.Lng,
                    row.Supplier.Latitude,
                    row.Supplier.Longitude),
            })
            .OrderBy(x => x.DistanceKm)

            // The tie-break is not defensive padding. Two suppliers at the same distance — the
            // same town, or simply the same rounded figure — leave OrderBy free to emit them in
            // whatever order the repository happened to produce, and the repository's order is
            // itself unspecified for rows with equal sort keys in Postgres. The visible symptom
            // is a supplier list that reshuffles between two identical requests, which reads as
            // a flickering UI bug rather than as a missing ORDER BY.
            .ThenBy(x => x.Row.Supplier.Id)
            .Take(limit)

            // Rounded for display only, after sorting. Sorting on the rounded value would let two
            // suppliers 40 metres apart swap places; sorting on the true distance and then
            // rounding keeps the order faithful to the geography.
            .Select(x => x.Row.ToDto(Math.Round(x.DistanceKm, DistanceDecimals, MidpointRounding.AwayFromZero)))
            .ToList();

        // The origin is echoed rather than left to the caller: the frontend renders "within N km
        // of X" and centres the map on it, and re-deriving it client-side is how the list and the
        // map come to disagree about where the buyer is.
        return new NearbySuppliersDto(origin, ranked);
    }
}
