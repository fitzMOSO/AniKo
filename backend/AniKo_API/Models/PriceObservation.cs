namespace AniKo_API.Models;

/// <summary>
/// One crop's price in one region for one month. Feeds Market Price Trends.
/// <para>
/// The <c>?months=</c> range selector is a filter over this table, which is why the demo seed
/// has to carry a full twelve months per crop: a shorter run makes the 12-month option look
/// broken rather than empty.
/// </para>
/// </summary>
public class PriceObservation
{
    public int Id { get; set; }

    public int CropId { get; set; }

    public Crop? Crop { get; set; }

    /// <summary>
    /// The same pre-composed "Municipality, Province" string as <c>Supplier.Region</c>.
    /// </summary>
    public required string Region { get; set; }

    /// <summary>
    /// The first day of the month being reported. A <see cref="DateOnly"/> because a month is
    /// not an instant; normalising to the first of the month is what makes "one row per crop
    /// per region per month" checkable rather than a convention nobody enforces.
    /// </summary>
    public DateOnly Month { get; set; }

    /// <summary>PHP per kilogramme. <c>numeric(18,2)</c> — never a float.</summary>
    public decimal PricePerKg { get; set; }
}
