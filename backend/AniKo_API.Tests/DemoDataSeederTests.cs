using System.Text.RegularExpressions;
using AniKo_API.Data.Seed;
using AniKo_API.Models;

namespace AniKo_API.Tests;

/// <summary>
/// Asserts the shape and the reproducibility of the demo dataset, not the behaviour of a
/// database.
/// <para>
/// Nothing here touches Postgres, and that is deliberate rather than a shortcut: the builders on
/// <see cref="DemoDataSeeder"/> are pure functions returning lists, so the properties that
/// actually matter — twelve consecutive months, four distinct statuses, unique references,
/// coordinates on land — are decidable without a connection, and stay decidable in a CI job that
/// has no database. The transaction and the marker row are the only parts that need a live
/// server, and they are exercised by the startup path rather than duplicated here.
/// </para>
/// </summary>
public class DemoDataSeederTests
{
    // ── Determinism ──────────────────────────────────────────────────────────

    /// <summary>
    /// The whole point of the seeder. Two builds must be indistinguishable, or a screenshot of
    /// the dashboard means nothing and "it looked different yesterday" is unanswerable.
    /// </summary>
    [Fact]
    public void PriceObservationsAreIdenticalAcrossBuilds()
    {
        var first = DemoDataSeeder.BuildPriceObservations();
        var second = DemoDataSeeder.BuildPriceObservations();

        Assert.Equal(first.Count, second.Count);

        Assert.Equal(
            first.Select(o => (o.CropId, o.Region, o.Month, o.PricePerKg)),
            second.Select(o => (o.CropId, o.Region, o.Month, o.PricePerKg)));
    }

    [Fact]
    public void OrdersAreIdenticalAcrossBuilds()
    {
        var first = BuildOrders();
        var second = BuildOrders();

        Assert.Equal(
            first.Select(o => (o.Reference, o.QuantityKg, o.Status, o.EstimatedDelivery, o.CreatedAt)),
            second.Select(o => (o.Reference, o.QuantityKg, o.Status, o.EstimatedDelivery, o.CreatedAt)));
    }

    [Fact]
    public void ListingsAreIdenticalAcrossBuilds()
    {
        var first = BuildListings();
        var second = BuildListings();

        Assert.Equal(
            first.Select(l => (l.Name, l.CropId, l.PricePerKg, l.VolumeKg, l.IsFeatured, l.CreatedAt)),
            second.Select(l => (l.Name, l.CropId, l.PricePerKg, l.VolumeKg, l.IsFeatured, l.CreatedAt)));
    }

    /// <summary>
    /// The epoch is the single fixed point every timestamp is derived from, so it is pinned as a
    /// literal here. If someone replaces it with <c>DateTime.UtcNow</c>, this fails immediately
    /// rather than the dataset quietly becoming a moving target.
    /// </summary>
    [Fact]
    public void SeedEpochIsAFixedUtcInstant()
    {
        Assert.Equal(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), DemoDataSeeder.SeedEpoch);
        Assert.Equal(DateTimeKind.Utc, DemoDataSeeder.SeedEpoch.Kind);
    }

    /// <summary>
    /// Every seeded instant must be UTC-kinded, because Npgsql refuses an <c>Unspecified</c>
    /// <see cref="DateTime"/> for a <c>timestamp with time zone</c> column — a failure that
    /// would otherwise surface for the first time on a deploy.
    /// </summary>
    [Fact]
    public void EverySeededTimestampIsUtc()
    {
        var users = DemoDataSeeder.BuildUsers();
        var listings = BuildListings();
        var orders = BuildOrders();

        Assert.All(users, u => Assert.Equal(DateTimeKind.Utc, u.CreatedAt.Kind));
        Assert.All(listings, l => Assert.Equal(DateTimeKind.Utc, l.CreatedAt.Kind));
        Assert.All(orders, o => Assert.Equal(DateTimeKind.Utc, o.CreatedAt.Kind));
    }

    /// <summary>
    /// A source-level guard against the clock creeping back in. Cheap, and it catches the case
    /// the value assertions above cannot: a new builder added later that reads the clock for a
    /// field nothing else asserts on.
    /// </summary>
    [Fact]
    public void SeederSourceContainsNoNondeterministicCalls()
    {
        var source = File.ReadAllText(SeederSourcePath());

        // "DateTime.UtcNow" appears in the prose explaining why it is absent, so the check is
        // against code rather than the whole file: comment lines are stripped first.
        var code = string.Join(
            '\n',
            source
                .Split('\n')
                .Where(line =>
                {
                    var trimmed = line.TrimStart();
                    return !trimmed.StartsWith("//", StringComparison.Ordinal)
                        && !trimmed.StartsWith("///", StringComparison.Ordinal)
                        && !trimmed.StartsWith('*')
                        && !trimmed.StartsWith("<", StringComparison.Ordinal);
                }));

        Assert.DoesNotContain("DateTime.UtcNow", code, StringComparison.Ordinal);
        Assert.DoesNotContain("DateTime.Now", code, StringComparison.Ordinal);
        Assert.DoesNotContain("DateTimeOffset.UtcNow", code, StringComparison.Ordinal);
        Assert.DoesNotContain("Guid.NewGuid", code, StringComparison.Ordinal);
        Assert.DoesNotContain("Random.Shared", code, StringComparison.Ordinal);
    }

    // ── Price observations ───────────────────────────────────────────────────

    /// <summary>
    /// Exactly twelve per crop. The range selector's longest option is twelve months, so eleven
    /// makes that option render a short line that looks like a bug in the chart.
    /// </summary>
    [Fact]
    public void EachCropHasExactlyTwelvePriceObservations()
    {
        var byCrop = DemoDataSeeder.BuildPriceObservations()
            .GroupBy(o => o.CropId)
            .ToDictionary(g => g.Key, g => g.ToList());

        Assert.Equal(3, byCrop.Count);

        Assert.All(
            new[]
            {
                ReferenceData.CropIds.Rice,
                ReferenceData.CropIds.Corn,
                ReferenceData.CropIds.Vegetables,
            },
            cropId =>
            {
                Assert.True(byCrop.ContainsKey(cropId), $"No price series for crop {cropId}.");
                Assert.Equal(12, byCrop[cropId].Count);
            });
    }

    /// <summary>
    /// Consecutive, distinct, first-of-month, and ending on the epoch's month. A gap would draw
    /// as a straight segment across two months rather than as missing data.
    /// </summary>
    [Fact]
    public void PriceMonthsAreConsecutiveDistinctAndEndAtTheEpochMonth()
    {
        var expectedLast = new DateOnly(DemoDataSeeder.SeedEpoch.Year, DemoDataSeeder.SeedEpoch.Month, 1);

        foreach (var group in DemoDataSeeder.BuildPriceObservations().GroupBy(o => o.CropId))
        {
            var months = group.Select(o => o.Month).ToList();

            Assert.Equal(months.Count, months.Distinct().Count());
            Assert.All(months, m => Assert.Equal(1, m.Day));

            var ordered = months.OrderBy(m => m).ToList();

            // Emitted in chronological order already; the chart reads them in insertion order.
            Assert.Equal(ordered, months);

            for (var i = 1; i < ordered.Count; i++)
            {
                Assert.Equal(ordered[i - 1].AddMonths(1), ordered[i]);
            }

            Assert.Equal(expectedLast, ordered[^1]);
            Assert.Equal(expectedLast.AddMonths(-11), ordered[0]);
        }
    }

    /// <summary>
    /// The series has to move. A flat line is the failure this seeder exists to avoid, so
    /// "several distinct values" is asserted rather than assumed.
    /// </summary>
    [Fact]
    public void PriceSeriesActuallyMove()
    {
        foreach (var group in DemoDataSeeder.BuildPriceObservations().GroupBy(o => o.CropId))
        {
            var prices = group.Select(o => o.PricePerKg).ToList();

            Assert.True(
                prices.Distinct().Count() >= 8,
                $"Crop {group.Key} has only {prices.Distinct().Count()} distinct prices; the chart would look flat.");
        }
    }

    /// <summary>
    /// Prices stay in the bands the trade uses. A rice figure of 4.50 or 450 renders happily and
    /// is wrong in a way only a Filipino buyer would catch.
    /// </summary>
    [Theory]
    [InlineData(1, 45.0, 60.0)]
    [InlineData(2, 20.0, 30.0)]
    [InlineData(3, 40.0, 120.0)]
    public void PriceObservationsStayInPlausibleBands(int cropId, double floor, double ceiling)
    {
        var prices = DemoDataSeeder.BuildPriceObservations()
            .Where(o => o.CropId == cropId)
            .Select(o => o.PricePerKg);

        Assert.All(prices, p =>
        {
            Assert.InRange((double)p, floor, ceiling);
            Assert.Equal(p, decimal.Round(p, 2));
        });
    }

    [Fact]
    public void PriceObservationRegionsComeFromReferenceData()
    {
        Assert.All(
            DemoDataSeeder.BuildPriceObservations(),
            o => Assert.Contains(o.Region, ReferenceData.Regions));
    }

    // ── Orders ───────────────────────────────────────────────────────────────

    /// <summary>
    /// All four, because the orders table has a badge colour for each and a status with no demo
    /// row is a badge nobody looks at before it ships.
    /// </summary>
    [Fact]
    public void AllFourOrderStatusesArePresent()
    {
        var statuses = BuildOrders().Select(o => o.Status).Distinct().ToList();

        Assert.Equal(
            Enum.GetValues<OrderStatus>().OrderBy(s => s),
            statuses.OrderBy(s => s));
    }

    /// <summary>
    /// <c>orders.reference</c> carries a unique index, so a duplicate here is not a cosmetic
    /// problem — it aborts the seed transaction on deploy.
    /// </summary>
    [Fact]
    public void OrderReferencesAreUniqueAndWellFormed()
    {
        var references = BuildOrders().Select(o => o.Reference).ToList();

        Assert.Equal(references.Count, references.Distinct(StringComparer.Ordinal).Count());
        Assert.All(references, r => Assert.Matches(new Regex(@"^AK-\d{4}$"), r));
        Assert.All(references, r => Assert.True(r.Length <= 32, "Reference exceeds the 32-char column."));
        Assert.Equal("AK-1001", references[0]);
    }

    [Fact]
    public void OrdersAreBoughtByBuyersAndPointAtSeededListings()
    {
        var users = DemoDataSeeder.BuildUsers();
        var listings = DemoDataSeeder.BuildListings(DemoDataSeeder.BuildSuppliers(users));
        var orders = DemoDataSeeder.BuildOrders(users, listings);

        Assert.All(orders, o =>
        {
            Assert.NotNull(o.Buyer);
            Assert.Equal(UserRole.Buyer, o.Buyer!.Role);
            Assert.NotNull(o.Listing);
            Assert.Contains(o.Listing!, listings);
            Assert.True(o.QuantityKg >= o.Listing!.MinimumOrderKg, $"{o.Reference} is below its lot's minimum.");
            Assert.True(o.QuantityKg <= o.Listing.VolumeKg, $"{o.Reference} exceeds its lot's volume.");
        });
    }

    // ── Suppliers ────────────────────────────────────────────────────────────

    /// <summary>
    /// The Philippines bounding box. This exists for exactly one bug: transposed latitude and
    /// longitude. 121.17/14.21 is syntactically fine, sorts fine, and puts the pin in the Pacific
    /// — nothing else in the stack would notice.
    /// </summary>
    [Fact]
    public void SupplierCoordinatesAreInsideThePhilippines()
    {
        var suppliers = BuildSuppliers();

        Assert.All(suppliers, s =>
        {
            Assert.InRange(s.Latitude, 4.5, 21.5);
            Assert.InRange(s.Longitude, 116.0, 127.0);
        });
    }

    /// <summary>
    /// The bounding box alone would pass a set of pins all dropped on the same spot. Each
    /// supplier's coordinate has to be near the locality it claims.
    /// </summary>
    [Theory]
    [InlineData("Calamba, Laguna", 14.21, 121.17)]
    [InlineData("Balanga, Bataan", 14.68, 120.54)]
    [InlineData("Cabanatuan, Nueva Ecija", 15.49, 120.97)]
    [InlineData("Tarlac City, Tarlac", 15.48, 120.59)]
    [InlineData("Dagupan, Pangasinan", 16.04, 120.33)]
    [InlineData("La Trinidad, Benguet", 16.46, 120.59)]
    public void EachSupplierSitsOnItsOwnLocality(string region, double latitude, double longitude)
    {
        var supplier = Assert.Single(BuildSuppliers(), s => s.Region == region);

        Assert.True(
            Math.Abs(supplier.Latitude - latitude) < 0.05,
            $"{region} latitude {supplier.Latitude} is not near {latitude}.");

        Assert.True(
            Math.Abs(supplier.Longitude - longitude) < 0.05,
            $"{region} longitude {supplier.Longitude} is not near {longitude}.");
    }

    /// <summary>
    /// Nearby Verified Suppliers filters on <c>Verified</c>. If every seeded supplier passes,
    /// the filter is indistinguishable from no filter at all.
    /// </summary>
    [Fact]
    public void SuppliersCoverEveryRegionAndBothVerificationStates()
    {
        var suppliers = BuildSuppliers();

        Assert.Equal(
            ReferenceData.Regions.OrderBy(r => r, StringComparer.Ordinal),
            suppliers.Select(s => s.Region).OrderBy(r => r, StringComparer.Ordinal));

        Assert.Contains(suppliers, s => s.Verified);
        Assert.Contains(suppliers, s => !s.Verified);
    }

    /// <summary>
    /// Every supplier is operated by a farmer, and no two suppliers share one — the FK is
    /// <c>Restrict</c> and the relationship is one operator to one business in the UI.
    /// </summary>
    [Fact]
    public void EverySupplierIsOwnedByADistinctFarmer()
    {
        var users = DemoDataSeeder.BuildUsers();
        var suppliers = DemoDataSeeder.BuildSuppliers(users);

        Assert.All(suppliers, s =>
        {
            Assert.NotNull(s.AppUser);
            Assert.Equal(UserRole.Farmer, s.AppUser!.Role);
            Assert.Contains(s.AppUser, users);
        });

        Assert.Equal(suppliers.Count, suppliers.Select(s => s.AppUser).Distinct().Count());
        Assert.Contains(users, u => u.Role == UserRole.Buyer);
    }

    // ── Listings ─────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>GET /api/v1/listings/featured</c> must return something, and the unfiltered list must
    /// be longer than it — otherwise "featured" is decoration.
    /// </summary>
    [Fact]
    public void ListingsMixFeaturedAndUnfeaturedAcrossAllThreeCrops()
    {
        var listings = BuildListings();

        Assert.Contains(listings, l => l.IsFeatured);
        Assert.Contains(listings, l => !l.IsFeatured);

        Assert.Equal(
            new[] { ReferenceData.CropIds.Rice, ReferenceData.CropIds.Corn, ReferenceData.CropIds.Vegetables }.Order(),
            listings.Select(l => l.CropId).Distinct().Order());
    }

    /// <summary>
    /// Per-crop price bands again, this time on the lot cards. Rice at 20 PHP/kg would read as
    /// corn to anyone who buys it.
    /// </summary>
    [Theory]
    [InlineData(1, 45.0, 60.0)]
    [InlineData(2, 20.0, 30.0)]
    [InlineData(3, 40.0, 120.0)]
    public void ListingPricesAreInThePlausibleBandForTheirCrop(int cropId, double floor, double ceiling)
    {
        var prices = BuildListings().Where(l => l.CropId == cropId).Select(l => l.PricePerKg).ToList();

        Assert.NotEmpty(prices);
        Assert.All(prices, p => Assert.InRange((double)p, floor, ceiling));
    }

    /// <summary>
    /// The flag is a denormalisation of the supplier's state; the seed must not create the
    /// contradiction the column is only meant to preserve a historical record of.
    /// </summary>
    [Fact]
    public void ListingVerificationMatchesItsSupplier()
    {
        var listings = BuildListings();

        Assert.All(listings, l =>
        {
            Assert.NotNull(l.Supplier);
            Assert.Equal(l.Supplier!.Verified, l.Verified);
        });
    }

    /// <summary>
    /// Guards the max-length constraints the model declares, since a violation is a 500 on the
    /// very first deploy rather than a test failure.
    /// </summary>
    [Fact]
    public void SeededStringsFitTheirColumns()
    {
        Assert.All(DemoDataSeeder.BuildUsers(), u => Assert.True(u.Name.Length <= 128));
        Assert.All(BuildSuppliers(), s =>
        {
            Assert.True(s.Name.Length <= 128);
            Assert.True(s.Region.Length <= 128);
        });
        Assert.All(BuildListings(), l =>
        {
            Assert.True(l.Name.Length <= 128);
            Assert.True(l.Grade.Length <= 8);
        });
    }

    [Fact]
    public void SeedVersionIsPinned()
    {
        Assert.Equal("demo-v1", DemoDataSeeder.SeedVersion);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static List<Supplier> BuildSuppliers() =>
        DemoDataSeeder.BuildSuppliers(DemoDataSeeder.BuildUsers());

    private static List<Listing> BuildListings() =>
        DemoDataSeeder.BuildListings(BuildSuppliers());

    private static List<Order> BuildOrders()
    {
        var users = DemoDataSeeder.BuildUsers();
        return DemoDataSeeder.BuildOrders(users, DemoDataSeeder.BuildListings(DemoDataSeeder.BuildSuppliers(users)));
    }

    /// <summary>
    /// Walks up from the test binary to the repository's <c>backend</c> directory. Fragile only
    /// if the project layout moves, in which case the test says so loudly.
    /// </summary>
    private static string SeederSourcePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName, "AniKo_API", "Data", "Seed", "DemoDataSeeder.cs");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate DemoDataSeeder.cs from " + AppContext.BaseDirectory);
    }
}
