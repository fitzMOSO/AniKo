using System.Globalization;
using AniKo_API.Mapping;
using AniKo_API.Models;
using AniKo_API.Repositories;

namespace AniKo_API.Tests.Mapping;

/// <summary>
/// The mappers are the one layer in this backend where a mistake produces a 200, a clean log, and
/// a wrong page.
/// <para>
/// Everything else fails loudly. A broken query throws, a bad connection string refuses to boot, a
/// missing endpoint 404s. But a mapper that emits <c>"Confirmed"</c> where the frontend expects
/// <c>"confirmed"</c>, or puts longitude in the latitude slot, or sends the surrogate id where the
/// human reference belongs, produces a perfectly valid response that renders a perfectly wrong
/// dashboard. There is no exception to catch and nothing in either log. These tests are the only
/// place those mistakes can be caught, which is why they assert on specific values rather than on
/// "a DTO came back".
/// </para>
/// </summary>
public class DashboardMappersTests
{
    // -----------------------------------------------------------------------
    // Status keys
    // -----------------------------------------------------------------------

    /// <summary>
    /// Driven off <see cref="Enum.GetValues{T}"/> rather than four hardcoded cases, deliberately.
    /// </summary>
    /// <remarks>
    /// The point is what happens to the *fifth* status. <c>OrderStatus</c>'s own doc comment says
    /// adding one is a coordinated frontend-and-backend change, because the frontend's
    /// <c>STATUS</c> colour map has entries for exactly four and a miss renders an unstyled badge
    /// rather than throwing. A hardcoded four-case test would keep passing after a fifth is added
    /// and let that ship. Enumerating the enum makes the new member appear here automatically.
    /// </remarks>
    public static TheoryData<OrderStatus> AllOrderStatuses
    {
        get
        {
            var data = new TheoryData<OrderStatus>();
            foreach (var status in Enum.GetValues<OrderStatus>())
            {
                data.Add(status);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(AllOrderStatuses))]
    public void EveryOrderStatusMapsToALowercaseKey(OrderStatus status)
    {
        var key = status.ToStatusKey();

        Assert.Equal(status.ToString().ToLowerInvariant(), key);
        Assert.False(
            string.IsNullOrWhiteSpace(key),
            $"{status} produced an empty status key, which the frontend would use as a lookup " +
            "key into the badge colour map and the i18n bundle — both of which miss silently.");
        Assert.Equal(key, key.ToLowerInvariant());
    }

    [Fact]
    public void TheStatusKeysAreExactlyTheFourTheFrontendStyles()
    {
        // The companion to the enumeration test above. That one proves every member is lowercased;
        // this one pins the resulting *set*, so that adding, renaming or removing a status fails
        // here with a diff naming the offending key rather than passing quietly.
        var keys = Enum.GetValues<OrderStatus>()
            .Select(status => status.ToStatusKey())
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["confirmed", "delivered", "processing", "shipped"], keys);
    }

    // -----------------------------------------------------------------------
    // Dates
    // -----------------------------------------------------------------------

    [Fact]
    public void EstimatedDeliveryIsAnIsoDate()
    {
        var dto = RowFor(new DateOnly(2026, 8, 1)).ToDto();

        Assert.Equal("2026-08-01", dto.EstimatedDelivery);
    }

    /// <summary>
    /// The culture-independence claim in <c>DashboardMappers</c>, actually exercised.
    /// </summary>
    /// <remarks>
    /// <b>The stated reason for pinning invariant is wrong, and the pin is still necessary.</b>
    /// The mapper's comment says the <c>-</c> in <c>yyyy-MM-dd</c> is a date separator that a
    /// culture using <c>/</c> would substitute. In .NET it is not: <c>/</c> is the separator
    /// placeholder in a custom format string and <c>-</c> is a literal, so <c>de-DE</c> (separator
    /// <c>.</c>) and <c>en-US</c> (separator <c>/</c>) both emit <c>2026-08-01</c> whether or not a
    /// culture is passed. Those two cases are included below anyway, because that is what the
    /// comment claims and a reader should be able to see it checked.
    /// <para>
    /// The real hazard is the <i>calendar</i>, and it is far worse than a separator. <c>yyyy</c>
    /// renders the year in the culture's own calendar: under <c>th-TH</c> this date is the
    /// Buddhist year <c>2569-08-01</c>, and under <c>ar-SA</c> the Hijri <c>1448-02-18</c> — a
    /// different day and month as well. Both are well-formed <c>yyyy-MM-dd</c> strings that no
    /// parser would reject and no log would mention; the chart would simply plot the wrong dates
    /// for anyone whose server happened to boot with that culture. The final assertion proves the
    /// pin is load-bearing by showing what the unpinned call would have produced.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("de-DE")]     // Date separator '.', Gregorian.
    [InlineData("en-US")]     // Date separator '/', Gregorian.
    [InlineData("th-TH")]     // Buddhist calendar — the case with real teeth.
    [InlineData("ar-SA")]     // Umm al-Qura calendar.
    public void EstimatedDeliveryIsTheSameUnderAnyAmbientCulture(string cultureName)
    {
        var original = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);

            var dto = RowFor(new DateOnly(2026, 8, 1)).ToDto();

            Assert.Equal("2026-08-01", dto.EstimatedDelivery);
        }
        finally
        {
            // Restored in a finally because CurrentCulture is ambient state on the test runner's
            // thread. Leaking it would make an unrelated test elsewhere fail depending on
            // execution order — the worst kind of flake to diagnose.
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void TheInvariantPinIsLoadBearingUnderANonGregorianCalendar()
    {
        // Guards the test above from becoming a tautology. If some future runtime made the ambient
        // culture irrelevant to this format string, the theory would pass for reasons that have
        // nothing to do with the mapper, and the pin could be deleted without any test noticing.
        var original = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("th-TH");

            var unpinned = new DateOnly(2026, 8, 1).ToString("yyyy-MM-dd");

            Assert.Equal("2569-08-01", unpinned);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    // -----------------------------------------------------------------------
    // Recent orders
    // -----------------------------------------------------------------------

    [Fact]
    public void RecentOrderIdIsTheHumanReferenceAndNotANumber()
    {
        // The row carries no numeric id at all, so this cannot be asserted by comparing against
        // one. Assert the shape instead: a buyer quoting "AK-1003" to support can be helped, a
        // buyer quoting "7" cannot, and a numeric id here would be indistinguishable from correct
        // in a screenshot of the table.
        var dto = RowFor(new DateOnly(2026, 8, 1)).ToDto();

        Assert.Equal("AK-1003", dto.Id);
        Assert.False(
            int.TryParse(dto.Id, out _),
            "The recent-order id parsed as an integer, which means the surrogate key leaked into " +
            "the column the buyer reads and quotes.");
    }

    [Fact]
    public void RecentOrderCarriesTheListingAndSupplierNamesIntoTheRightFields()
    {
        // Two adjacent strings of the same type is the classic transposition site: swapping them
        // compiles, serialises, and renders a table where every product is a supplier.
        var dto = RowFor(new DateOnly(2026, 8, 1)).ToDto();

        Assert.Equal("Premium Jasmine Rice", dto.Product);
        Assert.Equal("Bataan Rice Growers", dto.Supplier);
        Assert.Equal(500, dto.QuantityKg);
    }

    [Fact]
    public void RecentOrderStatusIsLowercasedOnTheWayOut()
    {
        // ToStatusKey is tested directly above; this proves the mapper actually calls it rather
        // than passing Status.ToString() through, which is the mistake that produces "Confirmed"
        // on the wire and an unstyled badge on the page.
        var dto = RowFor(new DateOnly(2026, 8, 1)).ToDto();

        Assert.Equal("confirmed", dto.Status);
    }

    private static RecentOrderRow RowFor(DateOnly estimatedDelivery) =>
        new(
            Reference: "AK-1003",
            ListingName: "Premium Jasmine Rice",
            SupplierName: "Bataan Rice Growers",
            QuantityKg: 500,
            Status: OrderStatus.Confirmed,
            EstimatedDelivery: estimatedDelivery,
            CreatedAt: new DateTime(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc));

    // -----------------------------------------------------------------------
    // Featured lots
    // -----------------------------------------------------------------------

    [Fact]
    public void FeaturedLotCarriesMinimumOrderKgIntoMinOrderKg()
    {
        // The one deliberate rename in the mapper: the entity says MinimumOrderKg, the wire says
        // minOrderKg. VolumeKg and MinimumOrderKg are both ints sitting next to each other, so the
        // values are chosen far apart — a swap would show 250 kg lots with a 5,000 kg minimum,
        // which the card renders happily.
        var dto = FeaturedRow().ToDto();

        Assert.Equal(250, dto.MinOrderKg);
        Assert.Equal(5_000, dto.VolumeKg);
    }

    [Fact]
    public void FeaturedLotFlattensTheNamesAndStringifiesTheId()
    {
        var dto = FeaturedRow().ToDto();

        Assert.Equal("42", dto.Id);
        Assert.Equal("Premium Jasmine Rice", dto.Name);
        Assert.Equal("rice", dto.Crop);
        Assert.Equal("A", dto.Grade);
        Assert.Equal("Bataan Rice Growers", dto.Supplier);
        Assert.Equal("Balanga, Bataan", dto.Region);
        Assert.True(dto.Verified);
        Assert.Equal(52.50m, dto.PricePerKg);
    }

    private static FeaturedListingRow FeaturedRow() =>
        new(
            Id: 42,
            Name: "Premium Jasmine Rice",
            CropName: "rice",
            Grade: "A",
            SupplierName: "Bataan Rice Growers",
            Region: "Balanga, Bataan",
            Verified: true,
            VolumeKg: 5_000,
            MinimumOrderKg: 250,
            PricePerKg: 52.50m);

    // -----------------------------------------------------------------------
    // Suppliers and coordinates
    // -----------------------------------------------------------------------

    /// <summary>
    /// The transposition test. Latitude 14 and longitude 121 are chosen because they are
    /// unmistakably asymmetric: a swap puts the pin at 121°N, which does not exist, rather than
    /// somewhere merely wrong. Symmetric fixture values (10, 10) would pass a swapped mapper.
    /// </summary>
    [Fact]
    public void SupplierCoordinatesLandOnLatAndLngInThatOrder()
    {
        var supplier = SupplierFixture();

        var location = supplier.ToLatLng();

        Assert.Equal(14.6760, location.Lat);
        Assert.Equal(120.9640, location.Lng);
    }

    [Fact]
    public void NearbySupplierKeepsTheCoordinatePairAndCarriesTheDistanceThrough()
    {
        var row = new SupplierWithCrops(SupplierFixture(), ["corn", "rice"]);

        var dto = row.ToDto(distanceKm: 12.5);

        Assert.Equal("7", dto.Id);
        Assert.Equal("Bataan Rice Growers", dto.Name);
        Assert.Equal("Balanga, Bataan", dto.Region);
        Assert.Equal(14.6760, dto.Location.Lat);
        Assert.Equal(120.9640, dto.Location.Lng);
        Assert.True(dto.Verified);
        Assert.Equal(["corn", "rice"], dto.Crops);
        Assert.Equal(12.5, dto.DistanceKm);
    }

    [Fact]
    public void ASupplierWithNoListingsGetsAnEmptyCropListRatherThanNull()
    {
        // Legitimate, per the DTO's own note, and worth pinning: an empty chip row is correct,
        // whereas a null would be a NullReferenceException on the frontend's .map().
        var dto = new SupplierWithCrops(SupplierFixture(), []).ToDto(distanceKm: 0);

        Assert.NotNull(dto.Crops);
        Assert.Empty(dto.Crops);
    }

    private static Supplier SupplierFixture() =>
        new()
        {
            Id = 7,
            Name = "Bataan Rice Growers",
            Region = "Balanga, Bataan",
            Latitude = 14.6760,
            Longitude = 120.9640,
            Verified = true,
        };
}
