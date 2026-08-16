using AniKo_API.Models;

namespace AniKo_API.Repositories;

public interface ICropRepository : IRepository<Crop>
{
    /// <summary>
    /// Every crop name, sorted.
    /// </summary>
    /// <remarks>
    /// The price chart needs the full crop set, not just the crops that happen to have
    /// observations in the selected window. Deriving the series list from the observations means
    /// a crop with no data for the last three months vanishes from the legend rather than showing
    /// a gap, and the legend then changes shape as the user moves the range selector.
    /// </remarks>
    /// <param name="cancellationToken">Aborts the query when the request is abandoned.</param>
    Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken cancellationToken = default);
}
