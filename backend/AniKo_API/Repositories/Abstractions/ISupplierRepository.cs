using AniKo_API.Models;

namespace AniKo_API.Repositories;

public interface ISupplierRepository : IRepository<Supplier>
{
    /// <summary>
    /// Verified suppliers with their derived crop lists.
    /// </summary>
    /// <remarks>
    /// Unfiltered by distance on purpose. Distance is a computed value, not a column — Postgres
    /// cannot filter on it without PostGIS or an inline haversine, and at demo scale (single
    /// figures) fetching all verified suppliers and ranking them in the service is both simpler
    /// and honest about the cost. If this table ever reaches thousands of rows, the fix is a
    /// bounding-box pre-filter in SQL, not a cleverer service.
    /// </remarks>
    /// <param name="cancellationToken">Aborts the query when the request is abandoned.</param>
    Task<IReadOnlyList<SupplierWithCrops>> ListVerifiedWithCropsAsync(CancellationToken cancellationToken = default);
}
