using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AniKo_API.Tests.Endpoints;

/// <summary>
/// Pins the shape of a 400 in the hosted environment.
/// </summary>
/// <remarks>
/// Every other test in this suite runs through <see cref="ApiFactory"/>, which pins
/// UseEnvironment("Development"). That is deliberate and useful, but it put the whole
/// suite on one side of a branch that is not a constant:
/// <c>RouteHandlerOptions.ThrowOnBadRequest</c> defaults to <c>true</c> in Development
/// and <c>false</c> everywhere else.
///
/// With it false, a minimal API does not throw <c>BadHttpRequestException</c> when a
/// required parameter fails to bind — it sets 400 and returns, so nothing reaches
/// <c>GlobalExceptionHandler</c> and the response carries no body and no content type.
/// This was found on Render, not here: locally a request missing <c>lng</c> answered
/// 400 with a problem+json document naming the parameter; the deployed service answered
/// a bare 400. Both are 400, which is why no test and no log noticed.
///
/// These tests therefore boot in Production on purpose. They are the only ones that
/// would fail if the explicit ThrowOnBadRequest line in Program.cs were removed.
/// </remarks>
public class BadRequestShapeInProductionTests : IClassFixture<BadRequestShapeInProductionTests.ProductionApiFactory>
{
    private readonly ProductionApiFactory _factory;

    public BadRequestShapeInProductionTests(ProductionApiFactory factory) => _factory = factory;

    /// <summary>
    /// Boots the API as it is hosted.
    /// </summary>
    /// <remarks>
    /// A connection string is supplied even though nothing here reaches a database.
    /// It is needed because minimal APIs resolve a handler's injected services as part
    /// of building the argument list — <c>EndpointFilterInvocationContext.Arguments</c>
    /// contains them — which happens <em>before</em> any endpoint filter runs. So
    /// <c>AniKoDbContext</c> is constructed, and <c>ConnectionStringResolver</c> throws,
    /// on a request that <c>ValidationFilter</c> was always going to reject without
    /// querying anything. Development gets this from appsettings.Development.json;
    /// Production has no such file, so without this the rejection surfaces as a 500.
    ///
    /// It is injected through <c>ConfigureAppConfiguration</c> rather than
    /// <c>UseSetting</c> on purpose: appsettings layers on top of host configuration, so
    /// a UseSetting value is silently overridden, while an in-memory source added here
    /// lands last and wins. The host is never contacted — nothing in these tests queries.
    /// </remarks>
    public sealed class ProductionApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");

            builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Database:MigrateOnStartup"] = "false",
                    ["ConnectionStrings:DefaultConnection"] =
                        "Host=localhost;Port=5432;Database=aniko_unused;Username=none;Password=none",
                }));
        }
    }

    [Fact]
    public async Task MissingRequiredParameter_Returns400WithProblemJson()
    {
        var client = _factory.CreateClient();

        // lng is omitted. This is a binding failure, not a validation failure:
        // the validator never runs because the request record never materialises.
        var response = await client.GetAsync("/api/v1/suppliers/nearby?lat=14.6");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task MissingRequiredParameter_BodyNamesTheParameter()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/suppliers/nearby?lat=14.6");
        var body = await response.Content.ReadAsStringAsync();

        // The whole point of the fix: a caller can tell what it forgot. Asserting the
        // body is non-empty is the part that regresses if ThrowOnBadRequest goes back
        // to its default, since the framework's short-circuit writes nothing at all.
        Assert.NotEmpty(body);

        // Compared case-insensitively on purpose. The binder names the record property
        // — detail reads: Required parameter "double Lng" was not provided from query
        // string — while a validation failure keys its errors object on the camelCase
        // "lng" the caller actually typed. So the two kinds of 400 agree on status,
        // shape and media type but disagree on the spelling of the parameter, and a
        // client matching the name against its own query string exactly will miss on
        // one of them. Not worth reshaping the framework's message over; worth pinning
        // here so the divergence is a recorded fact rather than a later surprise.
        Assert.Contains("lng", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BindingAndValidationFailures_ShareTheSameMediaType()
    {
        var client = _factory.CreateClient();

        // Two different mechanisms produce these: the framework's binder raises the
        // first, ValidationFilter<T> returns the second. A client branching on the
        // media type to decide whether a body is a problem document must be able to
        // treat them alike.
        var binding = await client.GetAsync("/api/v1/suppliers/nearby?lat=14.6");
        var validation = await client.GetAsync("/api/v1/suppliers/nearby?lat=999&lng=120");

        Assert.Equal(HttpStatusCode.BadRequest, binding.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, validation.StatusCode);
        Assert.Equal(
            binding.Content.Headers.ContentType?.MediaType,
            validation.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ValidationFailure_StillReportsTheOffendingValueInProduction()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/suppliers/nearby?lat=999&lng=120");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(problem.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("lat", out var latErrors));
        Assert.Contains("999", latErrors[0].GetString());
    }
}
