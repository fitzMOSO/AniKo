using AniKo_API.Models;

namespace AniKo_API.Repositories;

public interface IListingRepository : IRepository<Listing>
{
    /// <summary>Featured listings only, newest first, capped at <paramref name="limit"/>.</summary>
    /// <param name="limit">Already validated to [1, 50] by the request validator.</param>
    /// <param name="cancellationToken">Aborts the query when the request is abandoned.</param>
    Task<IReadOnlyList<FeaturedListingRow>> ListFeaturedAsync(int limit, CancellationToken cancellationToken = default);
}
