using AniKo_API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AniKo_API.Data.Seed;

/// <summary>
/// The demo dataset: users, suppliers, listings, orders and twelve months of price
/// observations. Written once, inside one transaction, and never again.
/// <para>
/// Two properties are load-bearing and everything below is arranged around them.
/// </para>
/// <para>
/// <b>Idempotency is a marker row, not a row count.</b> The obvious guard —
/// <c>if (await db.Listings.AnyAsync()) return;</c> — is wrong in the one case that matters: a
/// seed interrupted halfway leaves listings behind with no orders, the guard then reports
/// "already seeded" forever, and the database stays permanently half-populated with no error
/// anywhere. The <see cref="SeedHistory"/> row is written in the <i>same transaction</i> as the
/// data it describes, so either both land or neither does. Bumping <see cref="SeedVersion"/> is
/// how you re-seed on purpose.
/// </para>
/// <para>
/// <b>Determinism is absolute.</b> There is no <c>DateTime.UtcNow</c>, no <c>Guid.NewGuid()</c>
/// and no <c>Random.Shared</c> anywhere in this file. Every instant is an offset from
/// <see cref="SeedEpoch"/> and the only randomness is a <see cref="Random"/> constructed with a
/// hardcoded seed. That is not tidiness: a seeder that reads the clock produces a different
/// dataset on every deploy, which makes "the chart looks wrong" unreproducible and makes a
/// screenshot in a bug report useless. Seeding two databases must produce identical rows.
/// </para>
/// </summary>
public static class DemoDataSeeder
{
    /// <summary>
    /// The idempotency key. Change it — <c>demo-v2</c>, and so on — to deliberately re-seed a
    /// database that already carries the previous version's rows.
    /// </summary>
    public const string SeedVersion = "demo-v1";

    /// <summary>
    /// The fixed "now" the whole dataset hangs off. Every timestamp below is written as an
    /// offset from this instant, which is what makes the output reproducible.
    /// <para>
    /// Explicitly <see cref="DateTimeKind.Utc"/>: Npgsql rejects a <c>DateTime</c> with
    /// <c>Kind.Unspecified</c> for a <c>timestamp with time zone</c> column at write time, so an
    /// unkinded literal here would be a runtime failure on first deploy rather than a compile
    /// error.
    /// </para>
    /// </summary>
    public static readonly DateTime SeedEpoch = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// The seed for the price-wander generator. Hardcoded, so the twelve-month series is the
    /// same series every time it is generated.
    /// </summary>
    private const int PriceRandomSeed = 20_260_801;

    /// <summary>The first order reference; subsequent orders count up from it.</summary>
    private const int FirstOrderNumber = 1001;

    /// <summary>Twelve, because the range selector's longest option is twelve months.</summary>
    public const int PriceHistoryMonths = 12;

    /// <summary>
    /// Writes the demo dataset if this database has not already had it.
    /// </summary>
    /// <remarks>
    /// The insert order is users → suppliers → listings → orders because every foreign key in
    /// the model is <see cref="DeleteBehavior.Restrict"/> and none of them is nullable. In
    /// practice EF sorts the graph itself from the navigation properties, which is exactly why
    /// the builders below link by reference rather than by pinned integer id: pinned ids would
    /// also desynchronise Postgres' identity sequences, so the first row the application itself
    /// inserted after a seed would collide on the primary key.
    /// <para>
    /// Crops are deliberately absent. They arrive with the migration's <c>HasData</c>, and
    /// inserting them here would duplicate reference data and violate the unique index on
    /// <c>crops.name</c>.
    /// </para>
    /// </remarks>
    public static async Task SeedAsync(AniKoDbContext db, ILogger logger, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(logger);

        if (await db.SeedHistory.AnyAsync(h => h.Version == SeedVersion, ct))
        {
            logger.LogInformation("Demo seed {Version} is already applied; nothing to write.", SeedVersion);
            return;
        }

        var users = BuildUsers();
        var suppliers = BuildSuppliers(users);
        var listings = BuildListings(suppliers);
        var orders = BuildOrders(users, listings);
        var observations = BuildPriceObservations();

        // One transaction over one SaveChanges. The marker is added to the same change tracker
        // as the data, so there is no window in which the marker exists and the data does not.
        var strategy = db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            db.AppUsers.AddRange(users);
            db.Suppliers.AddRange(suppliers);
            db.Listings.AddRange(listings);
            db.Orders.AddRange(orders);
            db.PriceObservations.AddRange(observations);
            db.SeedHistory.Add(new SeedHistory { Version = SeedVersion, AppliedAt = SeedEpoch });

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });

        logger.LogInformation(
            "Demo seed {Version} written: {Users} users, {Suppliers} suppliers, {Listings} listings, {Orders} orders, {Observations} price observations.",
            SeedVersion,
            users.Count,
            suppliers.Count,
            listings.Count,
            orders.Count,
            observations.Count);
    }

    // ── Users ────────────────────────────────────────────────────────────────

    /// <summary>
    /// One farmer per region — each one owns the supplier at the same index, which is the
    /// invariant <see cref="BuildSuppliers"/> depends on — followed by the buyers.
    /// </summary>
    public static List<AppUser> BuildUsers()
    {
        var farmerNames = new[]
        {
            "Marisol Bautista",
            "Renato Dizon",
            "Elena Marquez",
            "Jomar Castillo",
            "Teresita Ramos",
            "Nestor Balajadia",
        };

        var buyerNames = new[]
        {
            "Grace Villanueva",
            "Arnel Sy",
            "Portia Delgado",
        };

        var users = new List<AppUser>(farmerNames.Length + buyerNames.Length);

        for (var i = 0; i < farmerNames.Length; i++)
        {
            users.Add(new AppUser
            {
                Name = farmerNames[i],
                Role = UserRole.Farmer,

                // Mirrors the supplier's own verification at the same index; an operator with a
                // verified business and an unverified account would be a state the UI has no
                // way to render.
                Verified = SupplierSeeds[i].Verified,
                AvatarUrl = null,

                // Staggered so "member since" is not identical across the whole demo account list.
                CreatedAt = SeedEpoch.AddDays(-540 + (i * 21)),
            });
        }

        for (var i = 0; i < buyerNames.Length; i++)
        {
            users.Add(new AppUser
            {
                Name = buyerNames[i],
                Role = UserRole.Buyer,
                Verified = i != buyerNames.Length - 1,
                AvatarUrl = null,
                CreatedAt = SeedEpoch.AddDays(-300 + (i * 35)),
            });
        }

        return users;
    }

    /// <summary>The farmers, in the order <see cref="BuildUsers"/> emits them.</summary>
    public static List<AppUser> FarmersOf(IReadOnlyList<AppUser> users) =>
        users.Where(u => u.Role == UserRole.Farmer).ToList();

    /// <summary>The buyers, in the order <see cref="BuildUsers"/> emits them.</summary>
    public static List<AppUser> BuyersOf(IReadOnlyList<AppUser> users) =>
        users.Where(u => u.Role == UserRole.Buyer).ToList();

    // ── Suppliers ────────────────────────────────────────────────────────────

    /// <summary>
    /// A supplier's fixed facts. Coordinates are the real centres of the six localities in
    /// <see cref="ReferenceData.Regions"/> — not jitter around a single point — because the
    /// supplier list sorts by haversine distance from the buyer and a cluster of invented pins
    /// makes that ordering meaningless to look at.
    /// </summary>
    private sealed record SupplierSeed(
        string Region,
        string Name,
        double Latitude,
        double Longitude,
        bool Verified);

    /// <summary>
    /// Indexed in lockstep with <see cref="ReferenceData.Regions"/>. The region strings are read
    /// from that array rather than retyped, so a supplier's region and a price observation's
    /// region cannot drift apart by a typo.
    /// <para>
    /// Two of the six are unverified on purpose: Nearby Verified Suppliers filters on the flag,
    /// and a dataset in which every row passes the filter cannot demonstrate that the filter
    /// works.
    /// </para>
    /// </summary>
    private static readonly SupplierSeed[] SupplierSeeds =
    [
        new(ReferenceData.Regions[0], "Laguna Lakeside Growers", 14.2117, 121.1653, true),
        new(ReferenceData.Regions[1], "Bataan Rice Growers", 14.6761, 120.5363, true),
        new(ReferenceData.Regions[2], "Nueva Ecija Grain Cooperative", 15.4864, 120.9675, true),
        new(ReferenceData.Regions[3], "Tarlac Central Farms", 15.4755, 120.5963, false),
        new(ReferenceData.Regions[4], "Pangasinan Harvest Traders", 16.0433, 120.3333, true),
        new(ReferenceData.Regions[5], "Benguet Highland Vegetables", 16.4602, 120.5878, false),
    ];

    /// <summary>
    /// One supplier per region, each owned by the farmer at the same index in
    /// <see cref="BuildUsers"/>. Linked by navigation property, not by id — see the remarks on
    /// <see cref="SeedAsync"/>.
    /// </summary>
    public static List<Supplier> BuildSuppliers(IReadOnlyList<AppUser> users)
    {
        var farmers = FarmersOf(users);

        if (farmers.Count < SupplierSeeds.Length)
        {
            throw new InvalidOperationException(
                $"Expected at least {SupplierSeeds.Length} farmer users, found {farmers.Count}.");
        }

        return SupplierSeeds
            .Select((seed, i) => new Supplier
            {
                AppUser = farmers[i],
                Name = seed.Name,
                Region = seed.Region,
                Latitude = seed.Latitude,
                Longitude = seed.Longitude,
                Verified = seed.Verified,
                ThumbnailUrl = null,
            })
            .ToList();
    }

    // ── Listings ─────────────────────────────────────────────────────────────

    private sealed record ListingSeed(
        int SupplierIndex,
        int CropId,
        string Name,
        string Grade,
        int VolumeKg,
        decimal PricePerKg,
        int MinimumOrderKg,
        bool IsFeatured);

    /// <summary>
    /// Twelve lots across the six suppliers and the three crops. Prices are in PHP per kilo and
    /// sit in the bands the trade actually uses — rice 45–60, corn 20–30, vegetables 40–120 — so
    /// that a lot card with an implausible figure on it reads as a bug rather than as data.
    /// <para>
    /// Trade names are not crop names: "Dinorado Rice" and "Premium White Rice" are both crop
    /// <c>rice</c>. That distinction is the reason <c>Listing.Name</c> exists separately from
    /// <c>Crop.Name</c>, and a seed that ignored it would make the two look redundant.
    /// </para>
    /// </summary>
    private static readonly ListingSeed[] ListingSeeds =
    [
        new(1, ReferenceData.CropIds.Rice, "Premium White Rice", "A", 24_000, 58.50m, 500, true),
        new(1, ReferenceData.CropIds.Rice, "Dinorado Rice", "A", 12_000, 59.75m, 250, false),
        new(2, ReferenceData.CropIds.Rice, "Well-Milled Rice", "B", 40_000, 47.25m, 1_000, true),
        new(2, ReferenceData.CropIds.Corn, "Yellow Corn Grain", "A", 30_000, 24.50m, 1_000, false),
        new(3, ReferenceData.CropIds.Corn, "Feed-Grade Yellow Corn", "B", 55_000, 21.80m, 2_000, true),
        new(3, ReferenceData.CropIds.Rice, "Sinandomeng Rice", "A", 18_000, 54.00m, 500, false),
        new(4, ReferenceData.CropIds.Rice, "Long Grain Rice", "B", 16_000, 49.90m, 500, false),
        new(4, ReferenceData.CropIds.Corn, "Sweet Corn Kernels", "A", 8_000, 28.60m, 250, true),
        new(0, ReferenceData.CropIds.Vegetables, "Calamba Ampalaya", "A", 3_500, 62.00m, 100, false),
        new(0, ReferenceData.CropIds.Vegetables, "Lakeside Eggplant", "B", 5_000, 44.50m, 150, true),
        new(5, ReferenceData.CropIds.Vegetables, "Baguio Beans", "A", 4_200, 96.75m, 100, true),
        new(5, ReferenceData.CropIds.Vegetables, "Highland Broccoli", "A", 2_400, 118.00m, 80, false),
    ];

    /// <summary>
    /// Builds the lots. <c>Verified</c> is copied from the supplier rather than invented: the
    /// column is a denormalisation of exactly that fact, and a seed that set it independently
    /// would be seeding the inconsistency the column is supposed to preserve a record of.
    /// </summary>
    public static List<Listing> BuildListings(IReadOnlyList<Supplier> suppliers)
    {
        var listings = new List<Listing>(ListingSeeds.Length);

        for (var i = 0; i < ListingSeeds.Length; i++)
        {
            var seed = ListingSeeds[i];
            var supplier = suppliers[seed.SupplierIndex];

            listings.Add(new Listing
            {
                Supplier = supplier,
                CropId = seed.CropId,
                Name = seed.Name,
                Grade = seed.Grade,
                VolumeKg = seed.VolumeKg,
                PricePerKg = seed.PricePerKg,
                MinimumOrderKg = seed.MinimumOrderKg,
                PhotoUrl = null,
                Verified = supplier.Verified,
                IsFeatured = seed.IsFeatured,

                // Newest lot first when the list sorts by CreatedAt descending.
                CreatedAt = SeedEpoch.AddDays(-4 - (i * 3)).AddHours(9),
            });
        }

        return listings;
    }

    // ── Orders ───────────────────────────────────────────────────────────────

    private sealed record OrderSeed(int BuyerIndex, int ListingIndex, int QuantityKg, OrderStatus Status);

    /// <summary>
    /// Eight orders covering all four statuses twice over. All four appear because the orders
    /// table renders a differently-coloured badge per status and a status with no row in the
    /// demo data is a badge nobody ever sees before it ships.
    /// </summary>
    private static readonly OrderSeed[] OrderSeeds =
    [
        new(0, 0, 1_500, OrderStatus.Confirmed),
        new(1, 2, 4_000, OrderStatus.Processing),
        new(2, 4, 6_000, OrderStatus.Shipped),
        new(0, 7, 800, OrderStatus.Delivered),
        new(1, 10, 300, OrderStatus.Confirmed),
        new(2, 3, 2_500, OrderStatus.Processing),
        new(0, 9, 900, OrderStatus.Shipped),
        new(1, 5, 1_200, OrderStatus.Delivered),
    ];

    /// <summary>
    /// Builds the orders, newest first. References are <c>AK-1001</c> upward: derived from the
    /// index, so they are stable across runs and unique by construction — which matters, because
    /// <c>orders.reference</c> carries a unique index and a duplicate would fail the whole
    /// transaction on deploy rather than in a test.
    /// </summary>
    public static List<Order> BuildOrders(IReadOnlyList<AppUser> users, IReadOnlyList<Listing> listings)
    {
        var buyers = BuyersOf(users);
        var orders = new List<Order>(OrderSeeds.Length);

        for (var i = 0; i < OrderSeeds.Length; i++)
        {
            var seed = OrderSeeds[i];
            var createdAt = SeedEpoch.AddDays(-1 - (i * 5)).AddHours(3 + i);

            orders.Add(new Order
            {
                Reference = $"AK-{FirstOrderNumber + i}",
                Buyer = buyers[seed.BuyerIndex],
                Listing = listings[seed.ListingIndex],
                QuantityKg = seed.QuantityKg,
                Status = seed.Status,

                // Delivered orders are already in the past and pending ones are not, which is
                // what makes the status column and the date column agree with each other.
                EstimatedDelivery = DateOnly.FromDateTime(createdAt.AddDays(DeliveryOffsetDays(seed.Status))),
                CreatedAt = createdAt,
            });
        }

        return orders;
    }

    private static int DeliveryOffsetDays(OrderStatus status) => status switch
    {
        OrderStatus.Confirmed => 21,
        OrderStatus.Processing => 14,
        OrderStatus.Shipped => 7,
        OrderStatus.Delivered => 3,
        _ => 14,
    };

    // ── Price observations ───────────────────────────────────────────────────

    private sealed record PriceSeriesSeed(
        int CropId,
        string Region,
        decimal StartPrice,
        decimal Amplitude,
        decimal Drift,
        decimal Floor,
        decimal Ceiling);

    /// <summary>
    /// One series per crop, each pinned to the province that actually grows it, and each with a
    /// band it is allowed to wander inside.
    /// <para>
    /// One region per crop is not a simplification to fix later — it is what keeps the count at
    /// exactly twelve rows per crop. A second region would double the series the trends endpoint
    /// returns for one crop and the chart would draw the sum of two provinces as though it were
    /// one price.
    /// </para>
    /// </summary>
    private static readonly PriceSeriesSeed[] PriceSeriesSeeds =
    [
        new(ReferenceData.CropIds.Rice, ReferenceData.Regions[2], 51.00m, 1.80m, 0.35m, 45.00m, 60.00m),
        new(ReferenceData.CropIds.Corn, ReferenceData.Regions[3], 23.40m, 1.10m, 0.18m, 20.00m, 30.00m),
        new(ReferenceData.CropIds.Vegetables, ReferenceData.Regions[5], 71.00m, 7.50m, 1.20m, 40.00m, 120.00m),
    ];

    /// <summary>The first of the month, twelve months back, so the run ends on the epoch's month.</summary>
    public static DateOnly FirstHistoryMonth =>
        new DateOnly(SeedEpoch.Year, SeedEpoch.Month, 1).AddMonths(-(PriceHistoryMonths - 1));

    /// <summary>
    /// Twelve consecutive months per crop, ending on <see cref="SeedEpoch"/>'s month.
    /// <para>
    /// The prices wander rather than sitting flat or climbing in a straight line, and that is a
    /// requirement rather than decoration: the chart's whole job is to show movement, and a
    /// straight line is indistinguishable from a rendering bug. The wander comes from a
    /// <see cref="Random"/> with a hardcoded seed, so it is the same wander every time — an
    /// unseeded generator here would redraw the chart differently on every deploy.
    /// </para>
    /// </summary>
    public static List<PriceObservation> BuildPriceObservations()
    {
        var random = new Random(PriceRandomSeed);
        var firstMonth = FirstHistoryMonth;
        var observations = new List<PriceObservation>(PriceSeriesSeeds.Length * PriceHistoryMonths);

        foreach (var series in PriceSeriesSeeds)
        {
            var price = series.StartPrice;

            for (var month = 0; month < PriceHistoryMonths; month++)
            {
                if (month > 0)
                {
                    // Rounded to six places before it touches a decimal: the double is only ever
                    // used to pick a step, and letting an unrounded one into the running price
                    // would make the series depend on floating-point representation.
                    var swing = (decimal)Math.Round((random.NextDouble() * 2.0) - 1.0, 6);
                    price += (swing * series.Amplitude) + series.Drift;
                    price = Math.Clamp(price, series.Floor, series.Ceiling);
                }

                observations.Add(new PriceObservation
                {
                    CropId = series.CropId,
                    Region = series.Region,
                    Month = firstMonth.AddMonths(month),
                    PricePerKg = Math.Round(price, 2, MidpointRounding.AwayFromZero),
                });
            }
        }

        return observations;
    }
}
