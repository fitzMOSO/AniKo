using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

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
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("dataStore").GetString()));
    }
}
