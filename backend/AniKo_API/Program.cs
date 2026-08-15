using AniKo_API.Configuration;
using AniKo_API.Endpoints;
using AniKo_API.Middleware;
using Microsoft.AspNetCore.HttpOverrides;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var listenUrl = PlatformEnvironment.GetListenUrl(builder.Configuration);
if (listenUrl is not null)
{
    builder.WebHost.UseUrls(listenUrl);
}

var isHosted = PlatformEnvironment.IsHosted(builder.Configuration);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Render's proxy is not in a known network range; clearing these accepts it.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// First in the pipeline on purpose: anything downstream that reads the request
// scheme or client IP must see the values Render's edge forwarded, not the
// plain-HTTP hop between the proxy and this container.
app.UseForwardedHeaders();

app.UseExceptionHandler();

// TLS terminates at Render's edge. Redirecting inside the container sees plain
// HTTP and produces a redirect loop, so this only runs when self-hosted.
if (!isHosted)
{
    app.UseHttpsRedirection();
}

// Mounted in every environment on purpose: a deployed API that cannot be
// browsed is half a deliverable. Revisit if this ever serves private data.
app.MapOpenApi();
app.MapScalarApiReference();

app.MapHealthChecks("/health");
app.MapInfoEndpoints();

app.Run();

// Note: the .NET 10 Web SDK already emits a *public* Program class for top-level
// statements, so WebApplicationFactory<Program> resolves without any help here.
// Do not add `public partial class Program;` — the generated class is not declared
// partial, so a second declaration is a duplicate-definition error.
