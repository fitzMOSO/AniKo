using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AniKo_API.Configuration;

namespace AniKo_API.Tests;

public class HealthEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public HealthEndpointTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Root_ReturnsServiceIdentity()
    {
        var client = _factory.CreateClient();

        var payload = await client.GetFromJsonAsync<JsonElement>("/");

        Assert.Equal("AniKo API", payload.GetProperty("name").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("version").GetString()));

        // Pinned to the value, not merely to "not blank". The weaker assertion was here
        // for three phases and held perfectly while this endpoint reported
        // dataStore "None (skeleton)" against a live Postgres: a stale constant is
        // still a non-blank string. An endpoint whose job is to say what the service is
        // talking to is worth exactly as much as the strictness of its test.
        Assert.Equal("PostgreSQL", payload.GetProperty("dataStore").GetString());
    }

    [Fact]
    public async Task Root_ReportsTheBuildCommit()
    {
        var client = _factory.CreateClient();

        var payload = await client.GetFromJsonAsync<JsonElement>("/");

        // In-process there is no RENDER_GIT_COMMIT, so the honest answer is "local".
        // What this pins is that the field is present and populated at all — it exists
        // so a stale deploy can be spotted from outside, and a field that silently
        // disappeared would fail open, looking fine while answering nothing.
        Assert.Equal(
            PlatformEnvironment.UnknownBuild,
            payload.GetProperty("commit").GetString());
    }
}
