namespace AniKo_API.Models;

/// <summary>
/// A quantity of one crop offered by one supplier at one price. Feeds Featured Wholesale Lots.
/// </summary>
public class Listing
{
    public int Id { get; set; }

    /// <summary>
    /// What the seller calls this particular lot — "Premium White Rice", "Dinorado Rice",
    /// "Baguio Beans".
    /// <para>
    /// Separate from <see cref="Models.Crop.Name"/> and not derivable from it: the crop is the
    /// series key the price chart groups by, and one crop has many lots under trade names the
    /// buyer actually recognises. All three examples above are crop <c>rice</c> or
    /// <c>vegetables</c>. Both the lot card (<c>lots.title</c>) and the orders table
    /// (<c>orders.col_product</c>) render this string, and the orders table reads it through the
    /// listing rather than keeping a copy — a copy on the order would drift from the lot it
    /// points at.
    /// </para>
    /// </summary>
    public required string Name { get; set; }

    public int SupplierId { get; set; }

    public Supplier? Supplier { get; set; }

    public int CropId { get; set; }

    public Crop? Crop { get; set; }

    /// <summary>
    /// Trade grade as printed on the sack — a letter such as "A" or "B", not a score.
    /// A string rather than an enum because the grading vocabulary is the trade's, not ours,
    /// and a new grade should not need a deployment.
    /// </summary>
    public required string Grade { get; set; }

    /// <summary>Total volume on offer, in kilogrammes. Integer: nobody sells 0.4 kg wholesale.</summary>
    public int VolumeKg { get; set; }

    /// <summary>PHP per kilogramme. <c>numeric(18,2)</c> — never a float.</summary>
    public decimal PricePerKg { get; set; }

    /// <summary>Smallest order the supplier will accept, in kilogrammes.</summary>
    public int MinimumOrderKg { get; set; }

    /// <summary>Nullable: the lot card has a placeholder tile for a photoless lot.</summary>
    public string? PhotoUrl { get; set; }

    /// <summary>
    /// Whether the supplier behind this lot is verified, denormalised onto the lot because
    /// the card renders the badge per lot. Kept as its own column rather than joined at read
    /// time so that revoking a supplier's verification does not silently rewrite the history
    /// of what a buyer was shown.
    /// </summary>
    public bool Verified { get; set; }

    /// <summary>
    /// Merchandising, not a ranking — nothing in the data can compute it. Indexed, because
    /// <c>GET /api/v1/listings/featured</c> filters on it every call.
    /// </summary>
    public bool IsFeatured { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }
}
