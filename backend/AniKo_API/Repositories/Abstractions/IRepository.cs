namespace AniKo_API.Repositories;

// Note the namespace: `AniKo_API.Repositories`, matching the folder above this one rather than
// this folder. That is deliberate and is the reason splitting the interfaces out of the
// implementations cost nothing at the call sites.
//
// C# does not require namespaces to track directories — only the default-namespace convention
// and a habit do. Following the folder here would have renamed every one of these types to
// `AniKo_API.Repositories.Abstractions.*`, which means a `using` edit in every service, every
// validator and every test that touches a repository, plus a second `using` in each file that
// needs both an interface and an implementation. All of that churn would buy exactly nothing:
// the separation being expressed is "contracts here, EF over there", and the folder already
// says it. A namespace split would additionally imply the two halves could be referenced
// independently, which they cannot — they ship in one assembly.

/// <summary>
/// The read primitives every entity shares.
/// <para>
/// This interface is deliberately small, and it is worth saying why rather than letting the next
/// reader assume it is unfinished. A generic repository that tries to cover querying ends up
/// re-exporting <c>IQueryable</c>, at which point it has abstracted nothing and merely renamed
/// EF Core. The dashboard's real queries — "featured listings with their supplier and crop",
/// "verified suppliers plus the crops derivable from their listings" — are specific, they carry
/// specific includes and orderings, and each one lives on the specific interface beside this file
/// where its contract can be stated and tested. What is left over is genuinely shared, and that
/// is this.
/// </para>
/// </summary>
/// <typeparam name="T">An entity type mapped by <c>AniKoDbContext</c>.</typeparam>
public interface IRepository<T>
    where T : class
{
    /// <summary>Every row. Only safe because every table here is reference-sized or demo-sized.</summary>
    Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The row with this primary key, or <c>null</c>.
    /// </summary>
    /// <remarks>
    /// The <c>int</c> key is an assumption this signature cannot enforce: nothing constrains
    /// <typeparamref name="T"/> to have an <c>Id</c> at all, so the implementation resolves it by
    /// name and finds out at query-translation time rather than at compile time. Every entity in
    /// this model does have one, so it holds today. An <c>IEntity</c> interface carrying
    /// <c>int Id</c>, applied to the entity classes and used as a constraint here, is what would
    /// make it a compile error instead — worth doing if a keyless or composite-key entity is ever
    /// added, and not worth the churn before then.
    /// </remarks>
    Task<T?> FindAsync(int id, CancellationToken cancellationToken = default);
}
