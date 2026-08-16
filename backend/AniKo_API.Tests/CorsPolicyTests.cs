using System.Net.Http.Headers;
using AniKo_API.Configuration;
using Microsoft.Extensions.Configuration;

namespace AniKo_API.Tests;

/// <summary>
/// CORS is worth testing precisely because it fails silently on the server. A misconfigured policy
/// produces a healthy 200, a clean log, and a working <c>curl</c>; only the browser refuses the
/// response, and it does so in the frontend's console. Without these tests the first signal would
/// be a "the dashboard is broken" report pointing at the wrong repository.
/// </summary>
public class CorsPolicyResolveOriginsTests
{
    private static IConfiguration ConfigurationWith(params string[] origins)
    {
        var values = origins
            .Select((origin, index) => new KeyValuePair<string, string?>(
                $"{CorsPolicy.OriginsKey}:{index}", origin));

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void AcceptsAPlainOrigin()
    {
        var origins = CorsPolicy.ResolveOrigins(ConfigurationWith("https://aniko-agri.netlify.app"));

        Assert.Equal(["https://aniko-agri.netlify.app"], origins);
    }

    [Fact]
    public void AcceptsAnOriginWithAPort()
    {
        var origins = CorsPolicy.ResolveOrigins(ConfigurationWith("http://localhost:5173"));

        Assert.Equal(["http://localhost:5173"], origins);
    }

    [Fact]
    public void ReturnsEmptyWhenNothingIsConfigured()
    {
        // Not an exception: an API with no browser client is a legitimate configuration, and
        // failing to boot over it would be worse than serving nothing cross-origin.
        Assert.Empty(CorsPolicy.ResolveOrigins(new ConfigurationBuilder().Build()));
    }

    [Theory]
    // The exact thing a person copies out of the address bar.
    [InlineData("https://aniko-agri.netlify.app/overview")]
    [InlineData("https://aniko-agri.netlify.app/")]
    public void RejectsAnOriginCarryingAPathOrTrailingSlash(string origin)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => CorsPolicy.ResolveOrigins(ConfigurationWith(origin)));

        // Asserting the message names the corrected form, because the whole point of throwing
        // here is that the person reading it can fix it without knowing the CORS spec.
        Assert.Contains("https://aniko-agri.netlify.app", exception.Message);
        Assert.Contains("no path", exception.Message);
    }

    [Fact]
    public void RejectsSomethingThatIsNotAUriAtAll()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => CorsPolicy.ResolveOrigins(ConfigurationWith("aniko-agri.netlify.app")));

        Assert.Contains("absolute URI", exception.Message);
    }
}

/// <summary>
/// End-to-end checks through the real middleware pipeline. These would catch an ordering mistake
/// that the unit tests above cannot — a correct origin list is useless if <c>UseCors</c> sits
/// after the endpoints or behind an HTTPS redirect.
/// </summary>
public class CorsPipelineTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public CorsPipelineTests(ApiFactory factory) => _factory = factory;

    private const string AllowedOrigin = "http://localhost:5173";

    [Fact]
    public async Task PreflightFromAnAllowedOriginIsApproved()
    {
        var client = _factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "/health");
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        Assert.True(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "The preflight came back without Access-Control-Allow-Origin, so the browser will " +
            "block the real request even though the server considers this a success.");
        Assert.Equal(AllowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task ActualRequestFromAnAllowedOriginCarriesTheHeader()
    {
        var client = _factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", AllowedOrigin);

        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Equal(AllowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task RequestFromAnUnlistedOriginGetsNoHeader()
    {
        // The negative case matters as much as the positive one: a policy that approves everything
        // would pass the two tests above and quietly be AllowAnyOrigin.
        var client = _factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "https://not-the-dashboard.example.com");

        var response = await client.SendAsync(request);

        // Note the shape of a CORS rejection: the request still succeeds server-side. The header
        // is simply absent and the browser is the thing that refuses.
        response.EnsureSuccessStatusCode();
        Assert.False(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "An unlisted origin was approved, which means the policy is effectively open.");
    }
}
