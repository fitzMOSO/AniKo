using AniKo_API.Dtos;

namespace AniKo_API.Services;

/// <summary>Featured wholesale lots with their supplier and crop resolved.</summary>
public interface IFeaturedLotsService
{
    /// <param name="limit">Already validated to [1, 50].</param>
    /// <param name="cancellationToken">Aborts the underlying queries when the request is abandoned.</param>
    Task<FeaturedLotsDto> GetAsync(int limit, CancellationToken cancellationToken = default);
}
