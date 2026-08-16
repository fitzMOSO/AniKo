namespace AniKo_API.Services;

// Namespace deliberately `AniKo_API.Services`, not `...Services.Abstractions` — see the note in
// Repositories/Abstractions/IRepository.cs. Folders separate contracts from implementations;
// renaming the types would only tax every call site for the privilege.

/// <summary>
/// The stable identifiers for the four stat tiles.
/// </summary>
/// <remarks>
/// Constants rather than inline strings because these are a join key: the frontend's
/// <c>OverviewStat.key</c> selects the icon, the translation and the <c>upIsGood</c> rule for
/// each tile. A typo here does not break the endpoint — it produces a tile the frontend has no
/// entry for, which renders blank.
/// </remarks>
public static class StatKeys
{
    /// <summary>Orders not yet delivered.</summary>
    public const string ActiveOrders = "activeOrders";

    /// <summary>Total value of orders placed in the window, in PHP.</summary>
    public const string Spend = "spend";

    /// <summary>Distinct suppliers ordered from in the window.</summary>
    public const string Suppliers = "suppliers";

    /// <summary>Average market price per kg across all crops, in PHP.</summary>
    public const string AveragePrice = "avgPrice";

    /// <summary>
    /// The order the tiles are emitted in. The frontend renders them in received order, so this
    /// is layout, and a set would not preserve it.
    /// </summary>
    public static readonly IReadOnlyList<string> All =
        [ActiveOrders, Spend, Suppliers, AveragePrice];
}
