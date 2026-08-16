using System.Net;
using System.Text.Json;

namespace AniKo_API.Tests.Endpoints;

/// <summary>
/// HTTP-level tests for input rejection on the dashboard endpoints.
/// <para>
/// Every request in this file is expected to be <b>refused before the handler runs</b>, which is
/// why the class deliberately uses the plain <see cref="ApiFactory"/> (migrations off, no
/// Testcontainers, no <c>[Collection("postgres")]</c>). A rejected request never reaches the
/// database, so needing Docker here would be proof that the rejection is not happening where it is
/// claimed to happen. If one of these tests ever starts failing because Postgres is not running,
/// the bug is in the endpoint, not in the test environment.
/// </para>
/// <para>
/// The unit tests under <c>Validation/</c> already assert that each validator returns the right
/// failures for the right values. They cannot detect the failure mode this file exists for:
/// minimal APIs do not run FluentValidation automatically, so a validator can be correct,
/// registered in DI, and fully unit-tested while never executing on a single real request. Only a
/// test that goes through the pipeline can tell the difference.
/// </para>
/// </summary>
public sealed class DashboardEndpointsValidationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public DashboardEndpointsValidationTests(ApiFactory factory) => _factory = factory;

    private async Task<(HttpStatusCode Status, string? ContentType, string Body)> GetAsync(
        string url)
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();

        return (response.StatusCode, response.Content.Headers.ContentType?.MediaType, body);
    }

    // ------------------------------------------------------------------------------------
    // (1) Range validation rejects rather than clamps, and rejects with 400 rather than 500.
    // ------------------------------------------------------------------------------------

    /// <summary>
    /// The central promise of <c>DashboardRequestBounds</c>: out-of-range is a 400, never a clamp.
    /// A clamp would return 200 with a chart the caller did not ask for and no way to notice —
    /// <c>months=0</c> renders as "no price data", <c>months=25</c> renders as if 25 months
    /// existed. A 500 would be almost as bad in the other direction, telling the caller to retry
    /// something that can never succeed.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    [InlineData(-1)]
    [InlineData(999)]
    public async Task PriceTrends_OutOfRangeMonths_Returns400(int months)
    {
        var (status, _, _) = await GetAsync($"/api/v1/pricing/trends?months={months}");

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    /// <summary>
    /// The bounds are inclusive, so 1 and 24 must get past validation. This asserts
    /// <b>not-400</b> rather than 200 on purpose: these values are accepted, so the request
    /// continues into the service and then the database, which is not running here (the factory
    /// turns migrations off and nothing provisions Postgres for this class). The interesting
    /// question is only whether validation let them through, and an off-by-one in
    /// <c>InclusiveBetween</c> — the realistic bug — shows up as a 400 either way.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(24)]
    public async Task PriceTrends_BoundaryMonths_AreNotRejectedByValidation(int months)
    {
        var (status, contentType, _) = await GetAsync($"/api/v1/pricing/trends?months={months}");

        Assert.NotEqual(HttpStatusCode.BadRequest, status);

        // Belt and braces: whatever the outcome, it must not be a validation problem. This is the
        // assertion that would survive someone later giving this class a real database.
        Assert.NotEqual("application/problem+json", contentType);
    }

    /// <summary>
    /// Omitting <c>months</c> must be a valid request. A default that sat outside its own bounds
    /// would turn every parameterless dashboard load into a 400 — the loudest possible bug, but
    /// only if something actually exercises the default path over HTTP.
    /// </summary>
    [Fact]
    public async Task PriceTrends_OmittedMonths_IsNotRejectedByValidation()
    {
        var (status, _, _) = await GetAsync("/api/v1/pricing/trends");

        Assert.NotEqual(HttpStatusCode.BadRequest, status);
    }

    /// <summary>
    /// Three endpoints share one <c>limit</c> rule via <c>DashboardValidationRules.ValidLimit</c>.
    /// Sharing the rule is not the same as applying it: each endpoint has its own
    /// <c>.WithValidation&lt;T&gt;()</c> call, and a missing one on any single endpoint leaves that
    /// endpoint silently unbounded while the shared unit tests stay green. Hence every endpoint is
    /// driven separately here.
    /// </summary>
    [Theory]
    [InlineData("/api/v1/listings/featured", 0)]
    [InlineData("/api/v1/listings/featured", 51)]
    [InlineData("/api/v1/listings/featured", -1)]
    [InlineData("/api/v1/orders/recent", 0)]
    [InlineData("/api/v1/orders/recent", 51)]
    [InlineData("/api/v1/orders/recent", -1)]
    [InlineData("/api/v1/suppliers/nearby?lat=14.6&lng=121.0&", 0)]
    [InlineData("/api/v1/suppliers/nearby?lat=14.6&lng=121.0&", 51)]
    public async Task OutOfRangeLimit_Returns400(string path, int limit)
    {
        var separator = path.Contains('?') ? string.Empty : "?";

        var (status, _, _) = await GetAsync($"{path}{separator}limit={limit}");

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    /// <summary>
    /// Coordinates outside the globe. A latitude of 91 is not a rounding error — it is almost
    /// always a transposed pair (Manila is lat 14.6, lng 121.0, so swapping them yields lat=121)
    /// and the endpoint would otherwise answer confidently: every supplier returned, sorted, each
    /// with a plausible distance in kilometres, all measured from a point that does not exist.
    /// </summary>
    [Theory]
    [InlineData(91, 121.0)]
    [InlineData(-91, 121.0)]
    [InlineData(90.0001, 121.0)]
    [InlineData(14.6, 181)]
    [InlineData(14.6, -181)]
    [InlineData(121.0, 14.6)] // The classic transposition, caught because 121 > 90.
    public async Task NearbySuppliers_OutOfRangeCoordinates_Returns400(double lat, double lng)
    {
        var (status, _, _) = await GetAsync(
            $"/api/v1/suppliers/nearby?lat={lat}&lng={lng}");

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    /// <summary>
    /// The poles and the antimeridian are inclusive bounds and must not be rejected. Same
    /// not-400 reasoning as the months boundary test: these are accepted, so they proceed to a
    /// database that is not here.
    /// </summary>
    [Theory]
    [InlineData(90, 180)]
    [InlineData(-90, -180)]
    [InlineData(0, 0)]
    public async Task NearbySuppliers_BoundaryCoordinates_AreNotRejectedByValidation(
        double lat,
        double lng)
    {
        var (status, contentType, _) = await GetAsync(
            $"/api/v1/suppliers/nearby?lat={lat}&lng={lng}");

        Assert.NotEqual(HttpStatusCode.BadRequest, status);
        Assert.NotEqual("application/problem+json", contentType);
    }

    /// <summary>
    /// <c>ValidCoordinate</c> claims to reject non-finite doubles, and this verifies the claim
    /// end to end rather than trusting the <c>double.IsFinite</c> call in isolation. It matters
    /// because <c>NaN</c> and <c>Infinity</c> are literally parseable by invariant-culture
    /// <c>double.TryParse</c>, so they bind successfully and arrive at the validator as real
    /// values — this is what a frontend sends when it does arithmetic on an empty geolocation
    /// result. <c>NaN</c> is the dangerous one: every ordinary range comparison against it is
    /// false, so it can slip through or be caught by accident depending on how the rule is
    /// written. If binding ever stops accepting these spellings the request is refused earlier and
    /// this still passes, which is why the assertion is on the status and not on the body.
    /// </summary>
    [Theory]
    [InlineData("NaN", "121.0")]
    [InlineData("14.6", "NaN")]
    [InlineData("Infinity", "121.0")]
    [InlineData("-Infinity", "121.0")]
    [InlineData("14.6", "Infinity")]
    [InlineData("14.6", "-Infinity")]
    public async Task NearbySuppliers_NonFiniteCoordinates_Returns400(string lat, string lng)
    {
        var (status, _, _) = await GetAsync($"/api/v1/suppliers/nearby?lat={lat}&lng={lng}");

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    // ------------------------------------------------------------------------------------
    // (2) The 400 body is an RFC 9457 ValidationProblemDetails a caller can act on.
    // ------------------------------------------------------------------------------------

    /// <summary>
    /// A 400 whose body is <c>text/plain</c> or bare JSON forces every client to special-case it.
    /// The media type is the part of RFC 9457 that clients branch on.
    /// </summary>
    [Theory]
    [InlineData("/api/v1/pricing/trends?months=0")]
    [InlineData("/api/v1/listings/featured?limit=0")]
    [InlineData("/api/v1/orders/recent?limit=99")]
    [InlineData("/api/v1/suppliers/nearby?lat=91&lng=0")]
    public async Task ValidationFailure_IsProblemJson(string url)
    {
        var (status, contentType, body) = await GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("application/problem+json", contentType);

        var problem = JsonSerializer.Deserialize<JsonElement>(body);

        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.True(
            problem.TryGetProperty("errors", out _),
            "A ValidationProblemDetails must carry an `errors` object; without it the caller is " +
            "told only that something was wrong, not which parameter.");
    }

    /// <summary>
    /// <b>The error key must be the query parameter the caller typed.</b> FluentValidation's
    /// default property name is the C# name (<c>Months</c>, <c>Limit</c>, <c>Lat</c>), and the
    /// validators override it to the wire name precisely because a caller reading
    /// <c>errors.Months</c> has to guess that it corresponds to <c>?months=</c>. It usually does,
    /// but nothing guarantees it, and the guess gets harder the moment a property and a parameter
    /// stop matching. Removing an <c>OverridePropertyName</c> breaks no unit test that only checks
    /// messages, so it is checked here.
    /// </summary>
    [Theory]
    [InlineData("/api/v1/pricing/trends?months=0", "months")]
    [InlineData("/api/v1/listings/featured?limit=0", "limit")]
    [InlineData("/api/v1/orders/recent?limit=51", "limit")]
    [InlineData("/api/v1/suppliers/nearby?lat=91&lng=0", "lat")]
    [InlineData("/api/v1/suppliers/nearby?lat=0&lng=181", "lng")]
    public async Task ValidationFailure_KeysErrorsByTheQueryParameterName(
        string url,
        string expectedKey)
    {
        var (_, _, body) = await GetAsync(url);

        var errors = JsonSerializer.Deserialize<JsonElement>(body).GetProperty("errors");

        var keys = errors.EnumerateObject().Select(property => property.Name).ToArray();

        Assert.True(
            keys.Contains(expectedKey, StringComparer.Ordinal),
            $"Expected the errors object to be keyed by the query parameter '{expectedKey}' as " +
            $"the caller typed it. Got: [{string.Join(", ", keys)}]. A PascalCase key here is a " +
            "bug in the validator's OverridePropertyName, not in this test.");
    }

    /// <summary>
    /// The message has to name the value that was received. "months must be between 1 and 24" on
    /// its own leaves the caller checking their own code for what they sent; echoing the value
    /// closes the loop, and it is the difference between a log line that is actionable and one
    /// that is merely correct.
    /// </summary>
    [Theory]
    [InlineData("/api/v1/pricing/trends?months=0", "months", "0")]
    [InlineData("/api/v1/pricing/trends?months=25", "months", "25")]
    [InlineData("/api/v1/listings/featured?limit=51", "limit", "51")]
    [InlineData("/api/v1/suppliers/nearby?lat=91&lng=0", "lat", "91")]
    public async Task ValidationFailure_MessageNamesTheReceivedValue(
        string url,
        string key,
        string receivedValue)
    {
        var (_, _, body) = await GetAsync(url);

        var messages = JsonSerializer.Deserialize<JsonElement>(body)
            .GetProperty("errors")
            .GetProperty(key)
            .EnumerateArray()
            .Select(element => element.GetString() ?? string.Empty)
            .ToArray();

        Assert.NotEmpty(messages);

        var joined = string.Join(" | ", messages);

        Assert.Contains(key, joined, StringComparison.Ordinal);
        Assert.Contains(
            receivedValue,
            joined,
            StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------------------------
    // (3) GlobalExceptionHandler: a caller's mistake is a 400, not a 500.
    // ------------------------------------------------------------------------------------

    /// <summary>
    /// <b>The key regression test.</b> <c>lat</c> and <c>lng</c> have no default, so omitting them
    /// fails in model binding — before any endpoint filter runs — and surfaces as a
    /// <c>BadHttpRequestException</c> carrying its own 400. The exception handler previously
    /// answered 500 unconditionally, which meant the single most likely mistake against this
    /// endpoint (forgetting the origin) told the caller the server was broken and to retry
    /// something that could never succeed, and logged it at Error so real faults were buried under
    /// other people's typos.
    /// </summary>
    [Fact]
    public async Task NearbySuppliers_WithNoCoordinatesAtAll_Returns400Not500()
    {
        var (status, _, _) = await GetAsync("/api/v1/suppliers/nearby");

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    /// <summary>
    /// One coordinate is as unbound as none. Sending only <c>lat</c> is the shape a half-finished
    /// frontend produces, and it must not be answered with a 500 either.
    /// </summary>
    [Theory]
    [InlineData("/api/v1/suppliers/nearby?lat=14.6")]
    [InlineData("/api/v1/suppliers/nearby?lng=121.0")]
    [InlineData("/api/v1/suppliers/nearby?limit=5")]
    public async Task NearbySuppliers_WithAPartialOrigin_Returns400Not500(string url)
    {
        var (status, _, _) = await GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    /// <summary>
    /// The 400 body has to say which parameter was missing. A ProblemDetails whose detail is null
    /// is the same dead end as the old 500: the status says "you got it wrong", and nothing says
    /// what. The framework writes this message, so no caller input is echoed into the response.
    /// </summary>
    [Fact]
    public async Task NearbySuppliers_MissingCoordinates_DetailNamesTheParameter()
    {
        var (status, contentType, body) = await GetAsync("/api/v1/suppliers/nearby");

        Assert.Equal(HttpStatusCode.BadRequest, status);

        // Both kinds of 400 this API produces must carry the *same* media type.
        //
        // This line was written asserting `application/json` and flagged as a known defect:
        // GlobalExceptionHandler wrote its body with WriteAsJsonAsync, which stamps
        // `application/json`, while the validation-filter 400s above come back as
        // `application/problem+json` from TypedResults.ValidationProblem. Two responses with an
        // identical shape under two different labels — a client branching on the media type to
        // decide whether a body is a problem document would parse one and fall through on the
        // other. The handler now passes the content type explicitly, so this asserts the fix.
        Assert.Equal("application/problem+json", contentType);

        var problem = JsonSerializer.Deserialize<JsonElement>(body);

        Assert.Equal(400, problem.GetProperty("status").GetInt32());

        var detail = problem.TryGetProperty("detail", out var value) ? value.GetString() : null;

        Assert.False(
            string.IsNullOrWhiteSpace(detail),
            "The 400 must carry a detail; the 500 branch deliberately omits it, so an empty " +
            "detail here means the client-error branch was not taken.");
        Assert.Contains("lat", detail!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Non-numeric coordinates fail to bind rather than failing to be present, which is a
    /// different exception path with the same correct answer. This is what an unparsed form field
    /// looks like on the wire.
    /// </summary>
    [Theory]
    [InlineData("/api/v1/suppliers/nearby?lat=abc&lng=def")]
    [InlineData("/api/v1/suppliers/nearby?lat=14.6&lng=not-a-number")]
    [InlineData("/api/v1/suppliers/nearby?lat=&lng=")]
    [InlineData("/api/v1/pricing/trends?months=six")]
    [InlineData("/api/v1/listings/featured?limit=ten")]
    public async Task UnparseableParameters_Return400Not500(string url)
    {
        var (status, _, _) = await GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    /// <summary>
    /// The 500 branch must not have been widened while fixing the 400 branch: an internal fault
    /// still has to stay a 500 with no detail. That direction is covered by
    /// <c>ExceptionHandlingTests</c> against the handler in isolation; what is asserted here is
    /// the complement — that none of the caller-error responses above leak a stack trace or an
    /// exception type name into the body.
    /// </summary>
    [Theory]
    [InlineData("/api/v1/suppliers/nearby")]
    [InlineData("/api/v1/suppliers/nearby?lat=abc&lng=def")]
    [InlineData("/api/v1/pricing/trends?months=0")]
    public async Task ClientErrorBodies_DoNotLeakInternals(string url)
    {
        var (_, _, body) = await GetAsync(url);

        Assert.DoesNotContain("Exception", body, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", body, StringComparison.Ordinal);
        Assert.DoesNotContain("AniKo_API.Services", body, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------------------------
    // (4) The filter is actually wired to every endpoint that claims to validate.
    // ------------------------------------------------------------------------------------

    /// <summary>
    /// <b>The test that catches a deleted <c>.WithValidation&lt;T&gt;()</c>.</b>
    /// <para>
    /// FluentValidation does not run itself in minimal APIs. There is no <c>[ApiController]</c>
    /// model-state gate: <c>AddValidatorsFromAssemblyContaining</c> puts the validators in DI and
    /// stops there. So the whole chain — validator written, validator correct, validator
    /// registered, validator unit-tested — can be green while an endpoint accepts
    /// <c>months=999</c>, because the one line that connects the validator to the route is
    /// missing. Nothing else in the suite would notice.
    /// </para>
    /// <para>
    /// Each of the four validated endpoints is driven here with input its validator is known to
    /// reject, and each must answer 400. Deleting <c>.WithValidation&lt;T&gt;()</c> from any one of
    /// them fails exactly one case in this theory and names the route. The list is also the
    /// checklist for a fifth validated endpoint: add the route here when you add the filter there.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("/api/v1/pricing/trends?months=999")]
    [InlineData("/api/v1/suppliers/nearby?lat=999&lng=999&limit=999")]
    [InlineData("/api/v1/listings/featured?limit=999")]
    [InlineData("/api/v1/orders/recent?limit=999")]
    public async Task EveryValidatedEndpoint_RunsItsValidator(string url)
    {
        var (status, contentType, _) = await GetAsync(url);

        Assert.True(
            status == HttpStatusCode.BadRequest,
            $"{url} returned {(int)status} instead of 400. The most likely cause is that " +
            ".WithValidation<T>() is no longer attached to that endpoint: minimal APIs do not " +
            "invoke FluentValidation on their own, so the validator exists and never runs.");

        Assert.Equal("application/problem+json", contentType);
    }

    /// <summary>
    /// The one endpoint that takes no parameters must not have acquired a validation filter by
    /// copy-paste. <c>ValidationFilter&lt;T&gt;</c> throws when no argument of its type is bound —
    /// deliberately, so the mistake is loud — which would turn the overview tiles into a 500 for
    /// every caller. Not-400 and not-500 is the assertion; the real status depends on the database
    /// this class does not have.
    /// </summary>
    [Fact]
    public async Task OverviewStats_TakesNoParameters_AndIsNotGatedByAValidationFilter()
    {
        var (status, _, body) = await GetAsync("/api/v1/buyer/overview/stats");

        Assert.NotEqual(HttpStatusCode.BadRequest, status);
        Assert.DoesNotContain("ValidationFilter", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Unknown query parameters are ignored, not rejected. Worth pinning: a cache-buster or a
    /// UTM tag appended by an analytics wrapper must not turn a working dashboard call into a 400.
    /// </summary>
    [Fact]
    public async Task UnknownQueryParameters_AreIgnored()
    {
        var (status, _, _) = await GetAsync(
            "/api/v1/pricing/trends?months=6&utm_source=test&_=1234567890");

        Assert.NotEqual(HttpStatusCode.BadRequest, status);
    }
}
