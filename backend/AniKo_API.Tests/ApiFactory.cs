using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AniKo_API.Tests;

/// <summary>
/// Boots the API in-process for integration tests. Uses the Development environment
/// so behaviour matches a developer's local run rather than the hosted configuration.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }
}
