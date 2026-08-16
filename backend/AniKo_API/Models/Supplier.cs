namespace AniKo_API.Models;

/// <summary>
/// A selling business, shown in Nearby Verified Suppliers and named on every lot card.
/// </summary>
public class Supplier
{
    public int Id { get; set; }

    /// <summary>The <see cref="AppUser"/> who operates this supplier.</summary>
    public int AppUserId { get; set; }

    public AppUser? AppUser { get; set; }

    /// <summary>Trading name, e.g. "Bataan Rice Growers". Not the operator's personal name.</summary>
    public required string Name { get; set; }

    /// <summary>
    /// Municipality and province pre-composed for display, e.g. "Balanga, Bataan" —
    /// the exact shape <c>features/suppliers/types.ts</c> already renders. See the
    /// class comment on <c>Data/Seed/ReferenceData.Regions</c> for why this is a string
    /// and not a foreign key.
    /// </summary>
    public required string Region { get; set; }

    /// <summary>
    /// Decimal degrees. <c>double</c> rather than <c>decimal</c> deliberately: this is a
    /// measurement fed to a haversine distance calculation and a map pin, not money.
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>Decimal degrees. See <see cref="Latitude"/>.</summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Nearby Verified Suppliers filters on this, so the seed must contain at least one
    /// unverified supplier or the filter is untestable.
    /// </summary>
    public bool Verified { get; set; }

    /// <summary>Nullable: the supplier list falls back to an initial-based avatar.</summary>
    public string? ThumbnailUrl { get; set; }
}
