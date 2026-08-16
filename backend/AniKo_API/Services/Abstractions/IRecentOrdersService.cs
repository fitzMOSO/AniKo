using AniKo_API.Dtos;

namespace AniKo_API.Services;

/// <summary>The most recently placed orders.</summary>
public interface IRecentOrdersService
{
    /// <param name="limit">Already validated to [1, 50].</param>
    /// <param name="cancellationToken">Aborts the underlying queries when the request is abandoned.</param>
    Task<RecentOrdersDto> GetAsync(int limit, CancellationToken cancellationToken = default);
}
