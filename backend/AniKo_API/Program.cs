using AniKo_API.Configuration;
using AniKo_API.Data;
using AniKo_API.Endpoints;
using AniKo_API.Middleware;
using AniKo_API.Repositories;
using AniKo_API.Services;
using AniKo_API.Validation;
using FluentValidation;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
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

// ---- Data ----------------------------------------------------------------
// Resolved once, at startup. On Render this is the `postgresql://` URI injected
// from the database declared in render.yaml; locally it is the keyword string in
// appsettings.Development.json. The resolver throws when neither is present
// rather than defaulting to localhost — a deploy that quietly connects to
// nothing fails somewhere far away from the cause.
builder.Services.AddDbContext<AniKoDbContext>(options =>
    options.UseNpgsql(ConnectionStringResolver.Resolve(builder.Configuration)));

// ---- CORS ---------------------------------------------------------------
// The dashboard is served from Netlify and this API from Render, so every call
// the frontend makes is cross-origin. Origins come from configuration rather
// than a constant: deploy previews and a renamed site are a config change, not
// a rebuild. AllowAnyOrigin is deliberately not used — it is indistinguishable
// from a correct policy today, while every endpoint is public read-only data,
// and stops being defensible the moment one isn't.
builder.Services.AddCors(options =>
    options.AddPolicy(CorsPolicy.PolicyName, policy => policy
        .WithOrigins(CorsPolicy.ResolveOrigins(builder.Configuration))
        .AllowAnyHeader()
        .AllowAnyMethod()));

// ---- Repositories --------------------------------------------------------
// Scoped, matching AddDbContext's own lifetime. This is not a stylistic choice:
// each repository holds the AniKoDbContext injected into it, and a singleton
// repository would capture the first request's context and keep using it after
// that scope was disposed. The failure is an ObjectDisposedException on the
// second request, or — worse, if the context survives — one DbContext shared
// across concurrent requests, which is not thread-safe and corrupts its change
// tracker rather than throwing.
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IListingRepository, ListingRepository>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<IPriceObservationRepository, PriceObservationRepository>();
builder.Services.AddScoped<ICropRepository, CropRepository>();

// ---- Services ------------------------------------------------------------
// Scoped for the same reason, transitively: these hold repositories, and a
// singleton holding a scoped dependency is the captive-dependency bug. The DI
// container catches that one at startup in Development and would fail the boot,
// which is the good case — but only because ValidateScopes is on there and off
// in Production, so it is worth getting right rather than relying on the check.
//
// Note for anyone adding a service that queries two repositories: they share one
// scoped DbContext, and a DbContext permits exactly one active operation. Fan-out
// with Task.WhenAll compiles, passes every unit test against fakes, and throws
// InvalidOperationException the first time it runs against Postgres. Await them
// in sequence.
builder.Services.AddScoped<IOverviewStatsService, OverviewStatsService>();
builder.Services.AddScoped<IPriceTrendsService, PriceTrendsService>();
builder.Services.AddScoped<INearbySupplierService, NearbySupplierService>();
builder.Services.AddScoped<IFeaturedLotsService, FeaturedLotsService>();
builder.Services.AddScoped<IRecentOrdersService, RecentOrdersService>();

// The clock, injected rather than read from DateTime.UtcNow. The stat tiles
// compare a trailing 30-day window against the 30 days before it, and the price
// chart counts months back from "now" — logic that is untestable against an
// ambient clock, because the assertions would depend on the day the suite runs.
// Singleton because TimeProvider.System is stateless; tests substitute a frozen
// one without touching this file.
builder.Services.AddSingleton(TimeProvider.System);

// The dashboard's own clock, which is not the system clock. See IDashboardClock: every window
// on this dashboard is defined relative to "now", and reading that off the wall clock is only
// correct while data keeps arriving. The cache is a singleton because one page view is five
// separate requests and therefore five separate scopes; the clock is scoped because it needs a
// scoped repository.
builder.Services.AddSingleton<DashboardClockCache>();
builder.Services.AddScoped<IDashboardClock, DashboardClock>();

// ---- Validation ----------------------------------------------------------
// Scanned rather than registered one by one. The trade is deliberate: a hand
// written list is greppable but silently incomplete the day someone adds a
// validator and forgets the line — and an unregistered validator does not fail,
// it just never runs, so the endpoint accepts input nobody checked. Scanning
// makes "exists" and "is registered" the same fact.
builder.Services.AddValidatorsFromAssemblyContaining<PriceTrendsRequestValidator>();

// ThrowOnBadRequest is set explicitly because its default is not a constant: it
// is true in Development and false everywhere else. That default makes a
// parameter binding failure behave differently in production than in every test
// — ApiFactory pins UseEnvironment("Development"), so the whole suite runs on
// the true side of the branch. A request missing a required query parameter
// answered 400 with a problem+json body naming the parameter locally, and a 400
// with no body and no content type on Render. Both are 400, which is why
// nothing failed; but a client that reads the body to learn which parameter it
// forgot gets an explanation in development and silence in production, and the
// contract advertised by ProducesValidationProblem is only half true.
//
// Pinning it to true makes the two kinds of 400 — binding and validation —
// carry the same shape and the same media type in every environment, which is
// what GlobalExceptionHandler and ValidationFilter were written to guarantee.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Render's proxy is not in a known network range; clearing these accepts it.
    // KnownIPNetworks, not the deprecated KnownNetworks — the old property is typed
    // in terms of Microsoft's own IPNetwork rather than System.Net.IPNetwork, and
    // clearing either one has the same effect, so this is a rename with no
    // behavioural change to reason about.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// First in the pipeline on purpose: anything downstream that reads the request
// scheme or client IP must see the values Render's edge forwarded, not the
// plain-HTTP hop between the proxy and this container.
app.UseForwardedHeaders();

app.UseExceptionHandler();

// Before the endpoints, and before HTTPS redirection: a preflight OPTIONS that
// gets redirected never reaches the CORS middleware, and the browser reports it
// as a CORS failure rather than a redirect.
app.UseCors(CorsPolicy.PolicyName);

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
app.MapDashboardEndpoints();

// Before the first request, not on a background thread: a request served against
// a half-migrated schema is the thing this exists to prevent, and Render's health
// check must not see a 200 until the schema is known good. Failure throws out of
// here, the process exits non-zero, the deploy fails, and the previous version
// keeps serving.
//
// The switch is not a test convenience bolted on: an operator running migrations
// as a separate step wants it too. It defaults to true, so forgetting it in
// production is not a failure mode — you have to opt out on purpose.
if (builder.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    await DbInitializer.InitializeAsync(app.Services);
}

app.Run();

// Note: the .NET 10 Web SDK already emits a *public* Program class for top-level
// statements, so WebApplicationFactory<Program> resolves without any help here.
// Do not add `public partial class Program;` — the generated class is not declared
// partial, so a second declaration is a duplicate-definition error.
