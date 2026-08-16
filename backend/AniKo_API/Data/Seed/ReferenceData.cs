using AniKo_API.Models;

namespace AniKo_API.Data.Seed;

/// <summary>
/// The small, stable data the schema is meaningless without. Consumed by
/// <c>AniKoDbContext.OnModelCreating</c> through <c>HasData</c>, so it is baked into the
/// migration, arrives with <c>Migrate()</c>, and re-running is a no-op by construction.
/// <para>
/// Demo data — suppliers, listings, orders, twelve months of prices — deliberately does NOT
/// live here. Every edit to a <c>HasData</c> set rewrites a migration, and a demo dataset
/// churns; that belongs to <c>DemoDataSeeder</c> in Phase D.
/// </para>
/// </summary>
public static class ReferenceData
{
    /// <summary>
    /// Ids are pinned, and pinning is not merely a <c>HasData</c> requirement here — a crop id
    /// ends up in seeded price observations and listings, so letting it drift would silently
    /// re-point historical rows at a different commodity.
    /// </summary>
    public static class CropIds
    {
        public const int Rice = 1;
        public const int Corn = 2;
        public const int Vegetables = 3;
    }

    /// <summary>
    /// Exactly the three crops the frontend already has: the <c>crop.rice</c>, <c>crop.corn</c>
    /// and <c>crop.vegetables</c> i18n keys, the three <c>SERIES</c> colours, and the
    /// <c>SeriesKey</c> union that types both a lot's crop and a supplier's crop list. A fourth
    /// crop here would render as a chart series with no colour and an untranslated label, so the
    /// set is intentionally closed until the frontend opens it.
    /// </summary>
    public static IReadOnlyList<Crop> Crops { get; } =
    [
        new Crop { Id = CropIds.Rice, Name = "rice", Unit = "kg" },
        new Crop { Id = CropIds.Corn, Name = "corn", Unit = "kg" },
        new Crop { Id = CropIds.Vegetables, Name = "vegetables", Unit = "kg" },
    ];

    /// <summary>
    /// The canonical Philippine locality strings, in the "Municipality, Province" form the
    /// supplier list and the lot cards already render.
    /// <para>
    /// These are constants and NOT an entity, which is the one place this file departs from the
    /// plan's "crops, regions" phrasing. A <c>Region</c> table would earn its keep if anything
    /// navigated by region — a filter dropdown, a per-region page, a rename that has to
    /// propagate. Nothing does: <c>Supplier.Region</c> and <c>PriceObservation.Region</c> are
    /// both read straight to the screen, and a foreign key would add a join to every one of
    /// those reads in exchange for a rename nobody has asked for. Keeping the list here still
    /// gets the real benefit — the seeder draws from one array, so a supplier's region and a
    /// price observation's region cannot drift apart by a typo — without the join. If a region
    /// filter ever ships, this array is the migration's starting point.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> Regions { get; } =
    [
        "Calamba, Laguna",
        "Balanga, Bataan",
        "Cabanatuan, Nueva Ecija",
        "Tarlac City, Tarlac",
        "Dagupan, Pangasinan",
        "La Trinidad, Benguet",
    ];
}
