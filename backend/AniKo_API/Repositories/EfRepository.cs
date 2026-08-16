using AniKo_API.Data;
using Microsoft.EntityFrameworkCore;

namespace AniKo_API.Repositories;

/// <summary>
/// The EF Core implementation of the two primitives every entity shares.
/// <para>
/// This class exists to hold two decisions, not to save typing — five copies of a two-line
/// <c>ToListAsync</c> would have been cheaper than a base class if that were the point. The
/// decisions are: every read is untracked, and a lookup by id is a query rather than
/// <c>DbSet.FindAsync</c>. Both are stated once here so that the per-entity repositories below
/// cannot quietly disagree with each other about them.
/// </para>
/// </summary>
/// <typeparam name="T">An entity type mapped by <see cref="AniKoDbContext"/> whose primary key is
/// an <c>int</c> named <c>Id</c>. Every entity in this model is; see <see cref="FindAsync"/>.</typeparam>
public class Repository<T> : IRepository<T>
    where T : class
{
    protected Repository(AniKoDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        Db = db;
    }

    protected AniKoDbContext Db { get; }

    /// <summary>
    /// The untracked entry point for every query in this assembly.
    /// <para>
    /// Tracking exists to detect changes on the way back out, and nothing here ever writes. What
    /// it costs when unused is not theoretical: the change tracker keeps a snapshot of every row
    /// it hands out, so a dashboard request that reads listings, orders and suppliers leaves
    /// three graphs pinned to a scoped context for the life of the request, and identity
    /// resolution then makes a second read of the same id return the first read's instance rather
    /// than the database's current row.
    /// </para>
    /// </summary>
    protected IQueryable<T> Query() => Db.Set<T>().AsNoTracking();

    public virtual async Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken = default) =>
        await Query().ToListAsync(cancellationToken);

    /// <summary>
    /// The row with this primary key, or <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Deliberately not <c>DbSet.FindAsync</c>. That method's headline feature is that it returns
    /// an already-tracked instance without a round trip, which is precisely the behaviour the
    /// rest of this class is avoiding: it cannot be composed with <c>AsNoTracking</c>, so a
    /// <c>Find</c> here would be the one read in the repository layer that tracks, and it would
    /// return a stale instance if anything else in the same scope had loaded that row.
    /// <para>
    /// <c>EF.Property</c> rather than a generic key accessor because the alternative is either a
    /// per-entity override of this method or an interface constraint on a shared <c>IHasId</c>,
    /// and neither buys anything: the model has one shape of key, and a future entity that breaks
    /// that assumption fails here loudly at query-translation time rather than returning wrong
    /// rows.
    /// </para>
    /// </remarks>
    public virtual async Task<T?> FindAsync(int id, CancellationToken cancellationToken = default) =>
        await Query().FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id, cancellationToken);
}
