using AniKo_API.Dtos;

namespace AniKo_API.Services;

/// <summary>
/// The four stat tiles, each with its current figure and a change against the prior period.
/// </summary>
public interface IOverviewStatsService
{
    /// <param name="cancellationToken">Aborts the underlying queries when the request is abandoned.</param>
    Task<OverviewStatsDto> GetAsync(CancellationToken cancellationToken = default);
}
