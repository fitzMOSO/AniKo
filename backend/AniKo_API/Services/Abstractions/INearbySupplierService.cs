using AniKo_API.Dtos;

namespace AniKo_API.Services;

/// <summary>
/// Verified suppliers ranked by great-circle distance from a buyer's position.
/// </summary>
public interface INearbySupplierService
{
    /// <param name="origin">The buyer's position. Validated to real latitude/longitude ranges,
    /// and required rather than defaulted — <c>(0, 0)</c> is a valid coordinate, so a missing
    /// origin would rank Philippine suppliers by distance from the Gulf of Guinea.</param>
    /// <param name="limit">Already validated to [1, 50].</param>
    /// <param name="cancellationToken">Aborts the underlying queries when the request is abandoned.</param>
    Task<NearbySuppliersDto> GetAsync(
        LatLngDto origin,
        int limit,
        CancellationToken cancellationToken = default);
}
