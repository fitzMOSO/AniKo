using AniKo_API.Services;

namespace AniKo_API.Tests.Services;

/// <summary>
/// Checks the haversine against distances that can be verified on a map, plus the two arguments
/// that break it numerically.
/// </summary>
public class GeoDistanceTests
{
    private const double ManilaLat = 14.5995;
    private const double ManilaLng = 120.9842;
    private const double CebuLat = 10.3157;
    private const double CebuLng = 123.8854;

    /// <summary>
    /// Manila to Cebu is about 570 km great-circle. The tolerance is deliberately loose — this
    /// assertion is not measuring the sphere model's accuracy, it is catching the three mistakes
    /// that produce a plausible-looking number: swapped latitude and longitude arguments (which
    /// gives ~1,200 km here), degrees passed where radians are wanted, and a radius in metres.
    /// </summary>
    [Fact]
    public void KilometresBetween_ManilaToCebu_MatchesTheKnownDistance()
    {
        var km = GeoDistance.KilometresBetween(ManilaLat, ManilaLng, CebuLat, CebuLng);

        Assert.InRange(km, 560.0, 580.0);
    }

    /// <summary>Distance is symmetric; an asymmetric result means a sign slipped somewhere.</summary>
    [Fact]
    public void KilometresBetween_IsSymmetric()
    {
        var there = GeoDistance.KilometresBetween(ManilaLat, ManilaLng, CebuLat, CebuLng);
        var back = GeoDistance.KilometresBetween(CebuLat, CebuLng, ManilaLat, ManilaLng);

        Assert.Equal(there, back, 9);
    }

    /// <summary>
    /// The same point is zero kilometres away and, more importantly, is not <c>NaN</c>. This is
    /// the regression test for the clamp: an unclamped implementation can hand <c>Acos</c> an
    /// argument marginally greater than 1 for coincident points and return <c>NaN</c>, which then
    /// sorts arbitrarily and serialises as null without raising anything.
    /// </summary>
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(ManilaLat, ManilaLng)]
    [InlineData(-33.8688, 151.2093)]
    [InlineData(90.0, 0.0)]
    [InlineData(-90.0, 180.0)]
    public void KilometresBetween_SamePoint_IsExactlyZeroAndNeverNaN(double lat, double lng)
    {
        var km = GeoDistance.KilometresBetween(lat, lng, lat, lng);

        Assert.False(double.IsNaN(km), "Coincident points produced NaN — the domain clamp is missing.");
        Assert.Equal(0.0, km);
    }

    /// <summary>
    /// Antipodal points are half the circumference apart, and they are the other half of the
    /// domain problem: the three haversine terms sum to marginally more than 1 here, so an
    /// unclamped <c>Asin(Sqrt(h))</c> is out of domain in exactly the case the formula is
    /// supposed to handle exactly.
    /// </summary>
    [Theory]
    [InlineData(0.0, 0.0, 0.0, 180.0)]
    [InlineData(45.0, 0.0, -45.0, 180.0)]
    [InlineData(90.0, 0.0, -90.0, 0.0)]
    [InlineData(14.5995, 120.9842, -14.5995, -59.0158)]
    public void KilometresBetween_AntipodalPoints_IsHalfTheCircumference(
        double lat1,
        double lng1,
        double lat2,
        double lng2)
    {
        var expected = Math.PI * GeoDistance.EarthRadiusKm;

        var km = GeoDistance.KilometresBetween(lat1, lng1, lat2, lng2);

        Assert.False(double.IsNaN(km), "Antipodal points produced NaN — the domain clamp is missing.");
        Assert.Equal(expected, km, 6);
    }

    /// <summary>
    /// A degree of latitude is about 111 km anywhere on the globe, which pins the radius
    /// independently of the two named cities above.
    /// </summary>
    [Fact]
    public void KilometresBetween_OneDegreeOfLatitude_IsAboutOneHundredAndElevenKilometres()
    {
        var km = GeoDistance.KilometresBetween(0.0, 0.0, 1.0, 0.0);

        Assert.InRange(km, 111.0, 111.5);
    }

    /// <summary>
    /// Longitude converges at the poles: a degree of longitude at 60° north spans half what it
    /// spans at the equator. This is the assertion that fails if <c>cos(phi)</c> is dropped from
    /// the longitude term — a bug that leaves equatorial distances perfect.
    /// </summary>
    [Fact]
    public void KilometresBetween_LongitudeConvergesTowardsThePole()
    {
        var atEquator = GeoDistance.KilometresBetween(0.0, 0.0, 0.0, 1.0);
        var atSixtyNorth = GeoDistance.KilometresBetween(60.0, 0.0, 60.0, 1.0);

        Assert.Equal(atEquator / 2.0, atSixtyNorth, 1);
    }

    /// <summary>The tuple overload is the same calculation, not a second one.</summary>
    [Fact]
    public void KilometresBetween_TupleOverload_AgreesWithTheScalarOverload()
    {
        var scalar = GeoDistance.KilometresBetween(ManilaLat, ManilaLng, CebuLat, CebuLng);
        var tuple = GeoDistance.KilometresBetween((ManilaLat, ManilaLng), (CebuLat, CebuLng));

        Assert.Equal(scalar, tuple);
    }
}
