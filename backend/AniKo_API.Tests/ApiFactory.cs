using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AniKo_API.Tests;

/// <summary>
/// Boots the API in-process for integration tests. Uses the Development environment
/// so behaviour matches a developer's local run rather than the hosted configuration.
/// </summary>
/// <remarks>
/// That choice has a cost worth naming: because every test here runs in Development,
/// this suite cannot see any framework default that branches on the environment.
/// One such default — <c>RouteHandlerOptions.ThrowOnBadRequest</c> — let a parameter
/// binding failure return a bare, bodiless 400 in production while returning a full
/// problem+json document in every test. See
/// <c>Endpoints/BadRequestShapeInProductionTests</c>, which boots in Production
/// precisely to cover the gap this class leaves.
/// </remarks>
public class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Migrations off. Without this, booting the app for a test that only asks
        // whether /health returns 200 would connect to Postgres and apply
        // migrations — quietly making the entire suite depend on a running
        // database, so it passes on the machine with a container up and fails
        // everywhere else.
        //
        // This turns off the *migration*, not the DbContext registration: the
        // connection string still has to resolve for the host to build, so a
        // resolver that stopped working would still fail these tests. Nothing
        // here connects, because nothing here queries.
        //
        // Integration tests that need real data (Phase F) get a Testcontainers
        // Postgres and turn this back on, rather than sharing a developer's box.
        builder.UseSetting("Database:MigrateOnStartup", "false");
    }
}
