namespace AniKo_API.Models;

/// <summary>
/// The idempotency marker for the demo seed. Phase D writes exactly one row inside the same
/// transaction as the data it describes.
/// <para>
/// It exists instead of <c>if (await db.Listings.AnyAsync()) return;</c>. That guard looks
/// right and fails badly: a seed killed halfway leaves some listings behind, the guard then
/// reports "already seeded", and the database stays permanently half-populated. Because this
/// row and the data share a transaction, either both land or neither does — and bumping
/// <see cref="Version"/> re-seeds on purpose.
/// </para>
/// </summary>
public class SeedHistory
{
    public int Id { get; set; }

    /// <summary>
    /// The version of the seed that produced the data, e.g. "demo-v1". Uniquely indexed so a
    /// concurrent second instance that slips past the advisory lock fails on the constraint
    /// rather than doubling every row.
    /// </summary>
    public required string Version { get; set; }

    /// <summary>UTC. Diagnostic only — it answers "when did this database last get its data?".</summary>
    public DateTime AppliedAt { get; set; }
}
