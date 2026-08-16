using AniKo_API.Dtos;
using AniKo_API.Mapping;
using AniKo_API.Repositories;

namespace AniKo_API.Services;

/// <summary>
/// Featured wholesale lots, ready for the wire.
/// </summary>
/// <remarks>
/// Thin to the point of looking pointless, so it is worth saying what it buys. The filtering
/// ("featured only"), the ordering ("newest first") and the cap all belong to the repository
/// because they are SQL; the field renames and the int-to-string id belong to the mappers because
/// they are the wire contract. What is left is the seam — and the seam is what lets the endpoint
/// depend on <see cref="IFeaturedLotsService"/> instead of on EF Core, and lets the day this
/// endpoint grows a currency conversion or a personalisation rule be a change to one class rather
/// than a change to a minimal API lambda.
/// </remarks>
public sealed class FeaturedLotsService(IListingRepository listings) : IFeaturedLotsService
{
    public async Task<FeaturedLotsDto> GetAsync(int limit, CancellationToken cancellationToken = default)
    {
        var rows = await listings.ListFeaturedAsync(limit, cancellationToken).ConfigureAwait(false);

        // No rows is an empty list, never null. The frontend maps over `lots` unguarded — a null
        // there is a TypeError that blanks the whole dashboard panel, whereas an empty array
        // renders the empty state that already exists for it.
        return new FeaturedLotsDto([.. rows.Select(row => row.ToDto())]);
    }
}
