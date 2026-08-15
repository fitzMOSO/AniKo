using System.Net;

namespace AniKo_API.Tests;

public class OpenApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public OpenApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task OpenApiDocument_IsServed()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ScalarReference_IsServed()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/scalar/v1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
