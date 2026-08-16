using AniKo_API.Dtos;
using AniKo_API.Validation;
using FluentValidation;
using FluentValidation.Results;

namespace AniKo_API.Tests.Validation;

/// <summary>
/// Shared helpers. Kept deliberately thin — these tests assert on <see cref="ValidationResult"/>
/// directly rather than through FluentValidation's test helper, because the thing under test is
/// partly the wording of the message and a fluent assertion that only reports "has an error for
/// Limit" would pass on a message that says nothing useful.
/// </summary>
public static class ValidationAssertions
{
    public static void AssertValid<T>(IValidator<T> validator, T request)
    {
        var result = validator.Validate(request);

        Assert.True(
            result.IsValid,
            $"Expected {request} to be accepted, but got: " +
            string.Join(" | ", result.Errors.Select(error => error.ErrorMessage)));
    }

    /// <summary>
    /// Asserts the request is rejected and returns the single failure, so the caller can inspect
    /// the message. Insisting on exactly one failure is intentional: two rules firing for the same
    /// value means a duplicated rule somewhere, and the caller would see a confusing double error.
    /// </summary>
    public static ValidationFailure AssertRejected<T>(IValidator<T> validator, T request)
    {
        var result = validator.Validate(request);

        Assert.False(
            result.IsValid,
            $"{request} was accepted. Out of range must be a 400, never a silent clamp — a " +
            "clamped value returns 200 with the wrong data and hides the caller's bug.");

        return Assert.Single(result.Errors);
    }
}

/// <summary>
/// <c>months</c> on <c>GET /api/v1/pricing/trends</c>.
/// <para>
/// Both failure modes here are invisible at runtime. Zero months draws an empty chart that reads
/// as "no price data available"; 999 months, under a clamping implementation, draws exactly the
/// same 24 months as a correct request and leaves the caller certain they are looking at four
/// years. Neither logs anything. The boundary cases below (0, 1, 24, 25) are where an
/// off-by-one in the rule would hide.
/// </para>
/// </summary>
public class PriceTrendsRequestValidatorTests
{
    private readonly PriceTrendsRequestValidator _validator = new();

    [Fact]
    public void TheDefaultRequestIsValid()
    {
        // The test this file exists to make impossible to forget. A default that sits outside its
        // own bounds turns *every* plain `GET /api/v1/pricing/trends` into a 400 — the endpoint
        // would fail for the one call shape the dashboard actually makes, while every explicit
        // months= value worked fine in manual testing.
        ValidationAssertions.AssertValid(_validator, new PriceTrendsRequest());

        Assert.InRange(
            DashboardRequestBounds.DefaultMonths,
            DashboardRequestBounds.MinMonths,
            DashboardRequestBounds.MaxMonths);
    }

    [Theory]
    [InlineData(1)]   // Lower boundary, inclusive.
    [InlineData(6)]
    [InlineData(23)]
    [InlineData(24)]  // Upper boundary, inclusive.
    public void AcceptsMonthsInsideTheRange(int months) =>
        ValidationAssertions.AssertValid(_validator, new PriceTrendsRequest(months));

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]     // Just below the lower boundary.
    [InlineData(25)]    // Just above the upper boundary.
    [InlineData(999)]
    [InlineData(int.MaxValue)]
    public void RejectsMonthsOutsideTheRange(int months) =>
        ValidationAssertions.AssertRejected(_validator, new PriceTrendsRequest(months));

    [Fact]
    public void TheMessageNamesTheParameterTheBoundsAndTheOffendingValue()
    {
        // Asserted as text, not as a boolean, because the message is the deliverable. A frontend
        // developer reading a 400 body should be able to fix the call without opening this repo,
        // which means it has to say which parameter, what range, and what was sent.
        var failure = ValidationAssertions.AssertRejected(_validator, new PriceTrendsRequest(25));

        Assert.Contains("'months'", failure.ErrorMessage);
        Assert.Contains("between 1 and 24", failure.ErrorMessage);
        Assert.Contains("inclusive", failure.ErrorMessage);
        Assert.Contains("Received 25", failure.ErrorMessage);
    }

    [Fact]
    public void TheErrorIsReportedAgainstTheQueryParameterName()
    {
        // `months`, not `Months`. The property name travels into the ProblemDetails `errors`
        // dictionary, and a frontend keying off the parameter it actually sent will not find a
        // PascalCase key.
        var failure = ValidationAssertions.AssertRejected(_validator, new PriceTrendsRequest(0));

        Assert.Equal("months", failure.PropertyName);
    }
}

/// <summary>
/// <c>limit</c> on the three list endpoints. One test class, run against all three validators, so
/// that a rule added to one and not the others fails here.
/// </summary>
public class LimitValidationTests
{
    /// <summary>
    /// The three validators, exercised as a set. Naming them individually in three near-identical
    /// classes would let one drift — the shared rule they call is only shared until somebody
    /// "fixes" one endpoint's cap in isolation.
    /// </summary>
    public static TheoryData<string, int, bool> LimitCases
    {
        get
        {
            var data = new TheoryData<string, int, bool>();
            int[] valid = [1, 2, 10, 49, 50];
            int[] invalid = [int.MinValue, -1, 0, 51, 1_000];

            foreach (var endpoint in Endpoints)
            {
                foreach (var limit in valid)
                {
                    data.Add(endpoint, limit, true);
                }

                foreach (var limit in invalid)
                {
                    data.Add(endpoint, limit, false);
                }
            }

            return data;
        }
    }

    private static readonly string[] Endpoints = ["featured", "recent", "nearby"];

    /// <summary>
    /// Runs one endpoint's validator at a given limit. The nearby-suppliers case is handed a real
    /// Manila coordinate so that the only thing under test is the limit.
    /// </summary>
    private static ValidationResult Validate(string endpoint, int limit) =>
        endpoint switch
        {
            "featured" => new FeaturedLotsRequestValidator().Validate(new FeaturedLotsRequest(limit)),
            "recent" => new RecentOrdersRequestValidator().Validate(new RecentOrdersRequest(limit)),
            "nearby" => new NearbySuppliersRequestValidator()
                .Validate(new NearbySuppliersRequest(14.676, 120.964, limit)),
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, null),
        };

    [Theory]
    [MemberData(nameof(LimitCases))]
    public void LimitIsBoundedIdenticallyOnEveryListEndpoint(string endpoint, int limit, bool expectedValid)
    {
        var result = Validate(endpoint, limit);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData("featured")]
    [InlineData("recent")]
    [InlineData("nearby")]
    public void TheLimitMessageNamesTheParameterAndTheRange(string endpoint)
    {
        var failure = Assert.Single(Validate(endpoint, 51).Errors);

        Assert.Equal("limit", failure.PropertyName);
        Assert.Contains("'limit'", failure.ErrorMessage);
        Assert.Contains("between 1 and 50", failure.ErrorMessage);
        Assert.Contains("Received 51", failure.ErrorMessage);
    }

    [Fact]
    public void TheDefaultLimitIsInsideItsOwnBounds()
    {
        // Same hazard as the months default: a plain request with no limit= must not 400.
        Assert.InRange(
            DashboardRequestBounds.DefaultLimit,
            DashboardRequestBounds.MinLimit,
            DashboardRequestBounds.MaxLimit);

        ValidationAssertions.AssertValid(new FeaturedLotsRequestValidator(), new FeaturedLotsRequest());
        ValidationAssertions.AssertValid(new RecentOrdersRequestValidator(), new RecentOrdersRequest());
        ValidationAssertions.AssertValid(
            new NearbySuppliersRequestValidator(),
            new NearbySuppliersRequest(14.676, 120.964));
    }
}

/// <summary>
/// <c>lat</c> and <c>lng</c> on <c>GET /api/v1/suppliers/nearby</c>.
/// <para>
/// A bad coordinate is the most confidently wrong input this API accepts. Nothing downstream
/// notices: the haversine happily computes a distance from anywhere to anywhere, the list comes
/// back sorted and labelled in kilometres, and the map centres on the origin it was given. The
/// only symptom is that the "nearest" suppliers are not near.
/// </para>
/// </summary>
public class NearbySuppliersRequestValidatorTests
{
    private readonly NearbySuppliersRequestValidator _validator = new();

    private static NearbySuppliersRequest At(double lat, double lng) => new(lat, lng, 10);

    [Theory]
    [InlineData(0, 0)]              // Null Island. A real coordinate, hence a valid request — and
                                    // exactly why the record gives lat/lng no default value.
    [InlineData(14.676, 120.964)]   // Manila, the realistic case.
    [InlineData(90, 180)]           // Both upper boundaries, inclusive.
    [InlineData(-90, -180)]         // Both lower boundaries, inclusive.
    [InlineData(90, -180)]
    [InlineData(-90, 180)]
    [InlineData(89.999999, 179.999999)]
    public void AcceptsRealCoordinates(double lat, double lng) =>
        ValidationAssertions.AssertValid(_validator, At(lat, lng));

    [Theory]
    [InlineData(90.000001)]     // Just past the pole.
    [InlineData(-90.000001)]
    [InlineData(91)]
    [InlineData(-91)]
    [InlineData(121)]           // The transposed pair: lat=121 is what `lat` and `lng` swapped
                                // looks like for a Philippine coordinate, and it is the single
                                // most likely bad latitude this endpoint will ever receive.
    [InlineData(180)]
    public void RejectsLatitudeOutsideThePoles(double lat)
    {
        var failure = ValidationAssertions.AssertRejected(_validator, At(lat, 120.964));

        Assert.Equal("lat", failure.PropertyName);
    }

    [Theory]
    [InlineData(180.000001)]
    [InlineData(-180.000001)]
    [InlineData(181)]
    [InlineData(-181)]
    [InlineData(360)]
    public void RejectsLongitudeOutsideTheAntimeridian(double lng)
    {
        var failure = ValidationAssertions.AssertRejected(_validator, At(14.676, lng));

        Assert.Equal("lng", failure.PropertyName);
    }

    /// <summary>
    /// <see cref="double.NaN"/> and the infinities, tested explicitly rather than assumed.
    /// </summary>
    /// <remarks>
    /// <c>NaN</c> is the value worth being deliberate about, because it fails every comparison
    /// including <c>NaN &gt;= -90</c>. A range rule written as two comparisons therefore rejects it
    /// by accident, and one written against <c>IComparable</c> (which orders <c>NaN</c> below
    /// everything) rejects it by a different accident. Both happen to be right today; neither is a
    /// thing to rely on. The rule states <see cref="double.IsFinite"/> and this test pins that
    /// decision so a later "simplification" back to <c>InclusiveBetween</c> is at least a
    /// deliberate one.
    /// <para>
    /// It is not hypothetical input either: <c>lat=NaN</c> is what a browser sends after doing
    /// arithmetic on an <c>undefined</c> geolocation reading, and <c>NaN</c> propagates through
    /// the haversine to produce a supplier list whose every distance is <c>NaN</c> and whose
    /// ordering is whatever the sort happened to leave behind.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void RejectsNonFiniteCoordinates(double value)
    {
        ValidationAssertions.AssertRejected(_validator, At(value, 120.964));
        ValidationAssertions.AssertRejected(_validator, At(14.676, value));
    }

    [Fact]
    public void TheCoordinateMessageNamesTheParameterAndTheDegreeRange()
    {
        var latFailure = ValidationAssertions.AssertRejected(_validator, At(121, 14));

        Assert.Contains("'lat'", latFailure.ErrorMessage);
        Assert.Contains("between -90 and 90 degrees", latFailure.ErrorMessage);
        Assert.Contains("Received 121", latFailure.ErrorMessage);

        var lngFailure = ValidationAssertions.AssertRejected(_validator, At(14, 200));

        Assert.Contains("'lng'", lngFailure.ErrorMessage);
        Assert.Contains("between -180 and 180 degrees", lngFailure.ErrorMessage);
        Assert.Contains("Received 200", lngFailure.ErrorMessage);
    }

    [Fact]
    public void TheMessageSaysFiniteSoNaNIsNotAMysteriousRejection()
    {
        var failure = ValidationAssertions.AssertRejected(_validator, At(double.NaN, 120.964));

        Assert.Contains("finite", failure.ErrorMessage);
    }

    [Fact]
    public void EveryBadFieldIsReportedNotJustTheFirst()
    {
        // FluentValidation continues across rules by default, and it matters here: a caller who
        // sent all three wrong should be told all three, not made to fix them one round trip at a
        // time.
        var result = _validator.Validate(new NearbySuppliersRequest(120, 200, 0));

        Assert.False(result.IsValid);
        Assert.Equal(
            ["lat", "lng", "limit"],
            result.Errors.Select(error => error.PropertyName).ToArray());
    }
}
