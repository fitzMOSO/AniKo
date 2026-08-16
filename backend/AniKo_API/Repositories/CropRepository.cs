using AniKo_API.Data;
using AniKo_API.Models;
using Microsoft.EntityFrameworkCore;

namespace AniKo_API.Repositories;

/// <inheritdoc cref="ICropRepository"/>
public sealed class CropRepository : Repository<Crop>, ICropRepository
{
    public CropRepository(AniKoDbContext db)
        : base(db)
    {
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken cancellationToken = default)
    {
        // Sorted in Postgres, not in memory, even though three rows would sort instantly either
        // way. The point is that the sort is part of the contract — the chart legend's order —
        // and a sort that lives in the query is one an EXPLAIN can show and a test can assert
        // without caring how many rows exist.
        //
        // No Distinct: crops.name carries a unique index, so a duplicate here would be a broken
        // constraint rather than something a query should quietly hide.
        return await Query()
            .OrderBy(c => c.Name)
            .Select(c => c.Name)
            .ToListAsync(cancellationToken);
    }
}
