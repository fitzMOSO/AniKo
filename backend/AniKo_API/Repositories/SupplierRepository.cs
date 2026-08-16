using AniKo_API.Data;
using AniKo_API.Models;
using Microsoft.EntityFrameworkCore;

namespace AniKo_API.Repositories;

/// <inheritdoc cref="ISupplierRepository"/>
public sealed class SupplierRepository : Repository<Supplier>, ISupplierRepository
{
    public SupplierRepository(AniKoDbContext db)
        : base(db)
    {
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <b>Two round trips, on purpose.</b> <see cref="Supplier"/> has no <c>Listings</c>
    /// collection — the relationship is configured <c>HasOne(...).WithMany()</c> with no
    /// navigation on this side — so there is nothing to <c>Include</c> and the crop list has to
    /// be assembled from the other end of the join.
    /// <para>
    /// The one-query alternative is a correlated collection projection:
    /// <c>Select(s =&gt; new { s, Crops = db.Listings.Where(l =&gt; l.SupplierId == s.Id)... })</c>.
    /// It is rejected for two reasons rather than one. It is fragile — <c>Distinct</c> followed
    /// by <c>OrderBy</c> inside a subquery is exactly the shape EF Core is most likely to refuse
    /// to translate, and a refusal is a runtime exception on a page, not a build error. And it is
    /// no cheaper: EF splits collection projections into a second command anyway, so the
    /// "one query" version is two commands whose count is decided by the provider instead of by
    /// this method.
    /// </para>
    /// <para>
    /// What matters is that neither trip is per supplier. The second query is a single scan of
    /// the listings belonging to the suppliers the first returned, and the fan-out happens in
    /// memory over at most (suppliers × crops) pairs — three crops exist. An
    /// <c>await</c> inside a <c>foreach</c> over suppliers would be the N+1 this is written to
    /// avoid, and it would look perfectly reasonable in a diff.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<SupplierWithCrops>> ListVerifiedWithCropsAsync(
        CancellationToken cancellationToken = default)
    {
        // Trip 1: the suppliers themselves. Ordered by name so the two demo suppliers that share
        // a distance band do not swap places between calls; the service re-sorts by distance
        // afterwards, and a stable input is what makes that sort reproducible.
        var suppliers = await Query()
            .Where(s => s.Verified)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

        if (suppliers.Count == 0)
        {
            return [];
        }

        var supplierIds = suppliers.Select(s => s.Id).ToList();

        // Trip 2: every (supplier, crop) pair those suppliers list, deduplicated and sorted by
        // Postgres. Distinct in SQL rather than in memory because a supplier with twenty rice
        // lots would otherwise transfer twenty copies of the string "rice" to produce one chip.
        var pairs = await Db.Listings
            .AsNoTracking()
            .Where(l => supplierIds.Contains(l.SupplierId))
            .Select(l => new { l.SupplierId, CropName = l.Crop!.Name })
            .Distinct()
            .OrderBy(p => p.SupplierId)
            .ThenBy(p => p.CropName)
            .ToListAsync(cancellationToken);

        var cropsBySupplier = pairs
            .GroupBy(p => p.SupplierId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(p => p.CropName).ToList());

        // Left join semantics, spelled out: a verified supplier with no listings keeps its place
        // in the result with an empty crop list. Building the result by iterating `pairs` instead
        // would drop it, and the symptom — one supplier missing from a list of six — is far
        // harder to trace back to this method than an empty chip row is.
        return suppliers
            .Select(s => new SupplierWithCrops(
                s,
                cropsBySupplier.TryGetValue(s.Id, out var crops) ? crops : []))
            .ToList();
    }
}
