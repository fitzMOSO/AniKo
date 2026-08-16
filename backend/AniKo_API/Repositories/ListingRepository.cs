using AniKo_API.Data;
using AniKo_API.Models;
using Microsoft.EntityFrameworkCore;

namespace AniKo_API.Repositories;

/// <inheritdoc cref="IListingRepository"/>
public sealed class ListingRepository : Repository<Listing>, IListingRepository
{
    public ListingRepository(AniKoDbContext db)
        : base(db)
    {
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<FeaturedListingRow>> ListFeaturedAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await Query()
            .Where(l => l.IsFeatured)
            .OrderByDescending(l => l.CreatedAt)
            .Take(limit)
            .Select(l => new FeaturedListingRow(
                l.Id,
                l.Name,

                // The crop's key, not the lot's trade name — "rice", not "Dinorado Rice". The
                // card renders both, and the client looks up a colour and a translation by this
                // one, so returning the trade name here would fail as a missing i18n key rather
                // than as anything a compiler could catch.
                l.Crop!.Name,
                l.Grade,
                l.Supplier!.Name,
                l.Supplier.Region,

                // The listing's own flag, not the supplier's. They are seeded equal, and the
                // column exists precisely so that they are allowed to diverge later — reading
                // through the join here would erase that distinction and rewrite the badge a
                // buyer was shown the moment a supplier's verification was revoked.
                l.Verified,
                l.VolumeKg,
                l.MinimumOrderKg,
                l.PricePerKg))
            .ToListAsync(cancellationToken);
    }
}
