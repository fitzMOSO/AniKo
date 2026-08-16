namespace AniKo_API.Models;

/// <summary>
/// A tradeable commodity. Reference data, seeded through <c>HasData</c> from
/// <c>Data/Seed/ReferenceData.cs</c> with pinned ids.
/// <para>
/// Both the price-trend series and the lot cards point at this row, which is the whole
/// point of the entity: the chart legend and a lot card cannot disagree about what
/// "rice" is if there is only one row that says so.
/// </para>
/// </summary>
public class Crop
{
    /// <summary>Pinned in <c>ReferenceData</c>; treat as a stable identifier, not a surrogate.</summary>
    public int Id { get; set; }

    /// <summary>
    /// The lowercase key the frontend already uses — <c>rice</c>, <c>corn</c>,
    /// <c>vegetables</c>. It matches the <c>crop.*</c> i18n keys and the <c>SERIES</c>
    /// colour map in <c>lib/chart-theme.ts</c>, so the client can look up a translation
    /// and a series colour from the value the API returns. A display-cased "Rice" here
    /// would force the client to lowercase it before either lookup worked.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Always <c>kg</c> today. It is stored rather than assumed so that a crop sold by
    /// the piece later is a data change, not a change to every price label in the UI.
    /// </summary>
    public required string Unit { get; set; }
}
