using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using AniKo_API.Configuration;
using AniKo_API.Data.Seed;
using AniKo_API.Models;
using AniKo_API.Services;
using AniKo_API.Tests.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AniKo_API.Tests.Endpoints;

/// <summary>
/// The five dashboard endpoints, exercised over real HTTP against the real Postgres the
/// repository suite already seeds.
/// <para>
/// The unit tests below <c>Services/</c> and <c>Mapping/</c> cover the same code paths against
/// fakes, so it is worth stating precisely what this file adds and they cannot: <b>everything
/// between the service and the socket</b>. Query-string binding through <c>[AsParameters]</c>,
/// the default values that live on a record's primary constructor, the validation filter, the
/// JSON naming policy, and the fact that all five routes are actually mapped. Every one of those
/// is invisible to a test that calls <c>service.GetAsync(...)</c> directly, and every one of them
/// fails as a 404, a 400, or — worst — a 200 carrying property names the frontend silently reads
/// as <c>undefined</c>.
/// </para>
/// <para>
/// Assertions are on data, not on status codes. A suite that only checks for 200 passes against
/// an endpoint returning an empty array, which is the exact shape a broken join produces.
/// Expected values are therefore recomputed from the seeded database through
/// <see cref="PostgresFixture.CreateContext"/>, so an edit to <see cref="DemoDataSeeder"/> moves
/// these expectations rather than breaking them.
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class DashboardEndpointsHappyPathTests : IDisposable
{
    /// <summary>
    /// A camelCase wire name: leading lowercase letter, no underscores, no PascalCase.
    /// </summary>
    /// <remarks>
    /// Asserted structurally rather than key by key because the failure mode is additive: someone
    /// adds a property to a DTO, ASP.NET Core serialises it, and nothing checks its casing until
    /// a frontend field renders blank. Walking the whole document means a new property is covered
    /// the day it appears.
    /// </remarks>
    private static readonly Regex CamelCase = new("^[a-z][A-Za-z0-9]*$", RegexOptions.Compiled);

    private readonly PostgresFixture _fixture;
    private readonly SeededApiFactory _factory;
    private readonly HttpClient _client;

    public DashboardEndpointsHappyPathTests(PostgresFixture fixture)
    {
        _fixture = fixture;

        // The container's connection string is read back off a context the fixture builds, since
        // that is the only handle the fixture exposes. Pointing the host at anything else — a
        // developer's local Postgres, say — would make these assertions depend on whatever
        // happens to be in that database.
        using (var db = fixture.CreateContext())
        {
            _factory = new SeededApiFactory(db.Database.GetConnectionString()!);
        }

        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    /// <summary>
    /// Boots the API against the collection fixture's already-migrated container.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A local class rather than a change to <see cref="ApiFactory"/> on purpose. That factory's
    /// whole value is that it boots without Docker — the health, CORS, OpenAPI and exception
    /// tests all rely on it — and teaching it about a container would quietly make the entire
    /// suite require a daemon.
    /// </para>
    /// <para>
    /// <b>Migrations stay off.</b> The fixture has already migrated and seeded this database, and
    /// <c>Migrate()</c> is not free to run twice concurrently: a second migrator racing the
    /// fixture's would deadlock on the history table rather than fail cleanly.
    /// </para>
    /// <para>
    /// <b><see cref="IWebHostBuilder.ConfigureAppConfiguration"/>, not <c>UseSetting</c>.</b> This
    /// is the trap in this file. <c>UseSetting</c> writes to host configuration, which
    /// <c>appsettings.Development.json</c> is layered <i>on top of</i> — and that file already
    /// sets <c>ConnectionStrings:DefaultConnection</c> to <c>localhost:55432</c>. A connection
    /// string supplied via <c>UseSetting</c> would therefore be silently overwritten, and every
    /// test here would fail against whatever is (or is not) listening on that port. An in-memory
    /// source added here lands last and wins.
    /// </para>
    /// <para>
    /// Both connection keys are set because <see cref="ConnectionStringResolver"/> prefers
    /// <c>DATABASE_URL</c> and falls back to the appsettings key; setting only one leaves the
    /// result dependent on whether the developer's shell happens to export the other. The
    /// container hands out an Npgsql keyword string, which the resolver passes through untouched.
    /// </para>
    /// </remarks>
    private sealed class SeededApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration(configuration =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [ConnectionStringResolver.PlatformUriKey] = connectionString,
                    [ConnectionStringResolver.FallbackKey] = connectionString,
                    ["Database:MigrateOnStartup"] = "false",
                }));
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// GETs, asserts 200, and returns both the parsed document and the raw body.
    /// </summary>
    /// <remarks>
    /// The raw text is kept because some of the assertions below are about the bytes rather than
    /// about the model: a deserialiser configured to be case-insensitive — which the web defaults
    /// are — will happily bind <c>PricePerKg</c> to <c>pricePerKg</c> and report a green test for
    /// a payload the frontend cannot read. Casing has to be checked before deserialisation
    /// forgives it.
    /// </remarks>
    private async Task<(JsonElement Root, string Raw)> GetOkAsync(string url)
    {
        var response = await _client.GetAsync(url);
        var raw = await response.Content.ReadAsStringAsync();

        // The body is included in the failure message because a 400 from the validation filter and
        // a 500 from a broken query are indistinguishable from a bare status-code assertion, and
        // the ProblemDetails payload names the parameter that was rejected.
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"GET {url} returned {(int)response.StatusCode}: {raw}");

        using var document = JsonDocument.Parse(raw);

        return (document.RootElement.Clone(), raw);
    }

    /// <summary>
    /// Asserts every property name in the document — at any depth — is camelCase.
    /// </summary>
    /// <remarks>
    /// Recursive rather than a flat check on the top level: the shapes that matter most are
    /// nested (<c>suppliers[].location.lng</c>, <c>points[].prices</c>), and the top level of
    /// every one of these responses is a single wrapper property that would pass on its own.
    /// <para>
    /// Crop names inside <c>prices</c> are dictionary <i>keys</i>, not property names, and they
    /// pass this check by construction because the reference data spells them lowercase. That is
    /// intentional overlap: they are also lookup keys into the frontend's chart theme, so a
    /// capitalised crop would be a real defect and this catches it for free.
    /// </para>
    /// </remarks>
    private static void AssertCamelCaseThroughout(JsonElement element, string path = "$")
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    Assert.True(
                        CamelCase.IsMatch(property.Name),
                        $"'{path}.{property.Name}' is not camelCase; the frontend reads this key verbatim.");

                    AssertCamelCaseThroughout(property.Value, $"{path}.{property.Name}");
                }

                break;

            case JsonValueKind.Array:
                var index = 0;

                foreach (var item in element.EnumerateArray())
                {
                    AssertCamelCaseThroughout(item, $"{path}[{index++}]");
                }

                break;
        }
    }

    // ── GET /api/v1/buyer/overview/stats ─────────────────────────────────────

    /// <summary>
    /// Four tiles, in <see cref="StatKeys.All"/> order, always.
    /// </summary>
    /// <remarks>
    /// The order is asserted as a <i>sequence</i> and not as a set, because it is layout: the
    /// dashboard renders a fixed four-column grid straight from this array, so swapping
    /// <c>spend</c> and <c>suppliers</c> puts a peso figure under a "Suppliers" heading. Nothing
    /// throws, nothing logs, and the grid still looks right.
    /// <para>
    /// The count is asserted separately from the sequence so a regression that drops a tile with
    /// an empty window (the reason the service emits zeros rather than omitting keys) reports
    /// "expected 4, got 3" rather than a sequence diff nobody reads.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task OverviewStatsReturnsExactlyFourTilesInLayoutOrder()
    {
        var (root, _) = await GetOkAsync("/api/v1/buyer/overview/stats");

        var stats = root.GetProperty("stats");
        var keys = stats.EnumerateArray().Select(s => s.GetProperty("key").GetString()).ToList();

        Assert.Equal(4, keys.Count);
        Assert.Equal(["activeOrders", "spend", "suppliers", "avgPrice"], keys);
    }

    /// <summary>
    /// The tile figures match what the seeded orders actually say, over the same trailing window
    /// the service defines.
    /// </summary>
    /// <remarks>
    /// This is the assertion that distinguishes "the endpoint is wired up" from "the endpoint is
    /// right". A handler that returned four tiles of zeros would satisfy every structural check
    /// in this file; only recomputing the figures from the rows catches it.
    /// <para>
    /// The window is recomputed from <c>DateTime.UtcNow</c> because the host resolves
    /// <see cref="TimeProvider.System"/> — this is the one place these tests cannot anchor on
    /// <see cref="PostgresFixture.SeedEpoch"/>, since the service under test is reading the wall
    /// clock. The seeded orders are spread five days apart, so the two clock reads straddling a
    /// window boundary is not a practical flake.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task OverviewStatsFiguresAgreeWithTheSeededOrders()
    {
        var (root, _) = await GetOkAsync("/api/v1/buyer/overview/stats");

        var stats = root.GetProperty("stats")
            .EnumerateArray()
            .ToDictionary(s => s.GetProperty("key").GetString()!, s => s.GetProperty("value").GetDecimal());

        await using var db = _fixture.CreateContext();

        var windowStart = DateTime.UtcNow.AddDays(-30);

        var window = await db.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt >= windowStart)
            .Select(o => new
            {
                o.Status,
                o.QuantityKg,
                o.Listing!.PricePerKg,
                SupplierId = o.Listing.SupplierId,
            })
            .ToListAsync();

        // "Active" is every status except Delivered — defined by exclusion in the service, so a
        // regression that switched to an inclusive list would drop a status added later.
        Assert.Equal(
            window.Count(o => o.Status != OrderStatus.Delivered),
            stats["activeOrders"]);

        Assert.Equal(window.Sum(o => o.QuantityKg * o.PricePerKg), stats["spend"]);
        Assert.Equal(window.Select(o => o.SupplierId).Distinct().Count(), stats["suppliers"]);

        // Spend is quantity times the *listing's* price, two joins from the orders table. Zero
        // here would mean the join collapsed — the shape a missing Include produces.
        Assert.True(stats["spend"] > 0m, "Spend is zero; the order → listing price join has broken.");
    }

    /// <summary>
    /// The average-price tile reads the latest month that actually has observations, not the
    /// current calendar month.
    /// </summary>
    /// <remarks>
    /// Market prices publish with a lag, so on any day before the month's figures land the two
    /// differ — and a tile that assumed the calendar month would show ₱0.00 with a 0% delta,
    /// which renders as a plausible "no change" rather than as missing data. Recomputed here from
    /// the same three-month lookback the service uses.
    /// </remarks>
    [Fact]
    public async Task OverviewStatsAveragePriceUsesTheLatestObservedMonth()
    {
        var (root, _) = await GetOkAsync("/api/v1/buyer/overview/stats");

        var avgPrice = root.GetProperty("stats")
            .EnumerateArray()
            .Single(s => s.GetProperty("key").GetString() == "avgPrice")
            .GetProperty("value")
            .GetDecimal();

        await using var db = _fixture.CreateContext();

        var now = DateTime.UtcNow;
        var lookbackStart = new DateOnly(now.Year, now.Month, 1).AddMonths(-2);

        var observations = await db.PriceObservations
            .AsNoTracking()
            .Where(o => o.Month >= lookbackStart)
            .Select(o => new { o.Month, o.PricePerKg })
            .ToListAsync();

        var expected = observations.Count == 0
            ? 0m
            : Math.Round(
                observations
                    .GroupBy(o => o.Month)
                    .OrderByDescending(g => g.Key)
                    .First()
                    .Average(o => o.PricePerKg),
                2,
                MidpointRounding.AwayFromZero);

        Assert.Equal(expected, avgPrice);
    }

    [Fact]
    public async Task OverviewStatsUsesCamelCaseWireNames()
    {
        var (root, raw) = await GetOkAsync("/api/v1/buyer/overview/stats");

        AssertCamelCaseThroughout(root);

        // Named explicitly as well as structurally: `deltaPercent` is the one property here whose
        // absence the frontend tolerates silently, rendering an empty chip instead of throwing.
        Assert.Contains("\"deltaPercent\"", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("\"DeltaPercent\"", raw, StringComparison.Ordinal);
    }

    // ── GET /api/v1/pricing/trends ───────────────────────────────────────────

    /// <summary>
    /// Omitting <c>months</c> yields six points.
    /// </summary>
    /// <remarks>
    /// <b>The highest-value structural test in this file.</b> The default lives on
    /// <c>PriceTrendsRequest</c>'s primary constructor and reaches the handler through
    /// <c>[AsParameters]</c>. Nothing guarantees minimal APIs honour a positional default that
    /// way — and when they do not, the parameter binds to <c>default(int)</c>, which is 0. Zero
    /// months is a 200 carrying an empty <c>points</c> array: an empty chart that reads as "no
    /// price data yet", not as a binding bug. It would also never reach the validator that
    /// rejects <c>months=0</c>, because the validator runs on the bound model and 0 is what
    /// binding produced.
    /// <para>
    /// Asserted as <c>6</c>, explicitly not as "greater than zero", because the second-worst
    /// outcome — binding to 1, or to the seeded twelve — also passes a laxer check while showing
    /// the user a window they did not select.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task PriceTrendsDefaultsToSixMonthsWhenMonthsIsOmitted()
    {
        var (root, _) = await GetOkAsync("/api/v1/pricing/trends");

        Assert.Equal(6, root.GetProperty("points").GetArrayLength());
    }

    /// <summary>
    /// An explicit <c>months</c> is honoured exactly — <c>3</c> means three points, not four.
    /// </summary>
    /// <remarks>
    /// The off-by-one is the real risk: the current month is one of the requested months, so the
    /// window opens <c>months - 1</c> back. Subtracting a full <c>months</c> yields an extra
    /// leading column that the "Last 3 months" label does not describe, and the chart still draws
    /// perfectly well.
    /// </remarks>
    [Fact]
    public async Task PriceTrendsHonoursAnExplicitMonthCount()
    {
        var (root, _) = await GetOkAsync("/api/v1/pricing/trends?months=3");

        Assert.Equal(3, root.GetProperty("points").GetArrayLength());
    }

    /// <summary>
    /// Points ascend by date, and the dates are consecutive whole months.
    /// </summary>
    /// <remarks>
    /// Ordering is contract, not incidental: Recharts plots points in array order, so a descending
    /// or shuffled series draws a scribble rather than raising anything. Consecutiveness is
    /// checked alongside it because a month axis derived from the data instead of from the clock
    /// would skip a month with no observations — compressing the x-axis so that a gap in the data
    /// becomes invisible instead of visible.
    /// </remarks>
    [Fact]
    public async Task PriceTrendsPointsAscendThroughConsecutiveMonths()
    {
        var (root, _) = await GetOkAsync("/api/v1/pricing/trends?months=6");

        var dates = root.GetProperty("points")
            .EnumerateArray()
            .Select(p => DateOnly.ParseExact(
                p.GetProperty("date").GetString()!,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture))
            .ToList();

        Assert.Equal(dates.OrderBy(d => d).ToList(), dates);

        for (var i = 1; i < dates.Count; i++)
        {
            Assert.Equal(dates[i - 1].AddMonths(1), dates[i]);
            Assert.Equal(1, dates[i].Day);
        }
    }

    /// <summary>
    /// Every point carries a key for every crop, and the values are the seeded observations.
    /// </summary>
    /// <remarks>
    /// The key-completeness half guards the frontend's hardest failure to diagnose: a
    /// <c>PricePoint</c> missing a crop makes Recharts drop that series from the legend for the
    /// <i>whole</i> range, so one blank month erases an entire line. The crop axis therefore has
    /// to come from the crops table rather than from the observations present.
    /// <para>
    /// The value half checks the pivot actually landed the right price in the right cell.
    /// Transposing crop and month produces a chart of exactly the right shape filled with the
    /// wrong numbers, which is not something a structural assertion can see. A month with no
    /// observation must be <c>0</c> — self-evidently not a price, since nothing trades at ₱0/kg —
    /// rather than a carried-forward value that would be indistinguishable from a flat market.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task PriceTrendsPivotsEverySeededCropIntoEveryPoint()
    {
        var (root, _) = await GetOkAsync("/api/v1/pricing/trends?months=12");

        await using var db = _fixture.CreateContext();

        var cropNames = await db.Crops.AsNoTracking().Select(c => c.Name).ToListAsync();

        // Three, from the migration's HasData. Zero here would mean the reference data never
        // arrived — the failure EnsureCreated instead of Migrate produces.
        Assert.Equal(3, cropNames.Count);

        var seeded = (await db.PriceObservations
                .AsNoTracking()
                .Join(db.Crops, o => o.CropId, c => c.Id, (o, c) => new { o.Month, Crop = c.Name, o.PricePerKg })
                .ToListAsync())
            .GroupBy(o => (o.Month, o.Crop))
            .ToDictionary(g => g.Key, g => Math.Round(g.Average(o => o.PricePerKg), 2, MidpointRounding.AwayFromZero));

        foreach (var point in root.GetProperty("points").EnumerateArray())
        {
            var month = DateOnly.ParseExact(
                point.GetProperty("date").GetString()!,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);

            var prices = point.GetProperty("prices");

            Assert.Equal(cropNames.Count, prices.EnumerateObject().Count());

            foreach (var crop in cropNames)
            {
                Assert.True(
                    prices.TryGetProperty(crop, out var price),
                    $"'{month:yyyy-MM-dd}' has no entry for crop '{crop}'; the series would vanish from the legend.");

                var expected = seeded.TryGetValue((month, crop), out var observed) ? observed : 0m;

                Assert.Equal(expected, price.GetDecimal());
            }
        }
    }

    [Fact]
    public async Task PriceTrendsUsesCamelCaseWireNames()
    {
        var (root, raw) = await GetOkAsync("/api/v1/pricing/trends");

        AssertCamelCaseThroughout(root);

        Assert.Contains("\"points\"", raw, StringComparison.Ordinal);
        Assert.Contains("\"prices\"", raw, StringComparison.Ordinal);

        // The date is a bare ISO day. A DateTime would serialise with a time and an offset the
        // chart's axis formatter would render as "Invalid Date" or, worse, silently shift a point
        // into the previous month for anyone east of UTC.
        Assert.DoesNotContain("T00:00:00", raw, StringComparison.Ordinal);
    }

    // ── GET /api/v1/suppliers/nearby ─────────────────────────────────────────

    /// <summary>Manila, near enough — a real origin with distinct distances to each supplier.</summary>
    private const double OriginLat = 14.5995;
    private const double OriginLng = 120.9842;

    /// <summary>
    /// The origin comes back exactly as it was asked for.
    /// </summary>
    /// <remarks>
    /// Two distinct failures hide here and both produce a confident wrong answer. If <c>lng</c>
    /// were spelled <c>lon</c> anywhere along the path it would bind to 0 — a valid coordinate in
    /// the Gulf of Guinea — and every distance would be measured from there while the list still
    /// rendered, still sorted, still labelled in kilometres. If the pair were transposed on the
    /// way out, the map would centre on a point in China while the list stayed correct. Asserting
    /// the echo is exact catches both; asserting it is merely "present" catches neither.
    /// </remarks>
    [Fact]
    public async Task NearbySuppliersEchoesTheRequestedOriginExactly()
    {
        var (root, _) = await GetOkAsync(
            FormattableString.Invariant($"/api/v1/suppliers/nearby?lat={OriginLat}&lng={OriginLng}"));

        var origin = root.GetProperty("origin");

        Assert.Equal(OriginLat, origin.GetProperty("lat").GetDouble());
        Assert.Equal(OriginLng, origin.GetProperty("lng").GetDouble());
    }

    /// <summary>
    /// Only verified suppliers, ranked nearest first, with the distances the geometry gives.
    /// </summary>
    /// <remarks>
    /// The seed deliberately leaves two of six suppliers unverified, so "returns four" is a real
    /// filter assertion rather than a count that would hold either way — and the two excluded are
    /// named below so a filter inverted to <c>!Verified</c> fails loudly instead of returning a
    /// plausible list of two.
    /// <para>
    /// The expected ordering is recomputed with <see cref="GeoDistance"/> rather than hardcoded.
    /// The two nearest suppliers to Manila sit roughly 47 km and 49 km away — close enough that a
    /// hardcoded order would be asserting an arithmetic coincidence, while recomputing asserts
    /// what actually matters: that the ranking reaching the wire is the ranking of the true
    /// distances, and that rounding to one decimal happened *after* the sort rather than before.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task NearbySuppliersReturnsOnlyVerifiedSuppliersNearestFirst()
    {
        var (root, _) = await GetOkAsync(
            FormattableString.Invariant($"/api/v1/suppliers/nearby?lat={OriginLat}&lng={OriginLng}"));

        var returned = root.GetProperty("suppliers").EnumerateArray().ToList();

        await using var db = _fixture.CreateContext();

        var expected = (await db.Suppliers
                .AsNoTracking()
                .Where(s => s.Verified)
                .Select(s => new { s.Id, s.Name, s.Latitude, s.Longitude })
                .ToListAsync())
            .Select(s => new
            {
                s.Id,
                s.Name,
                DistanceKm = GeoDistance.KilometresBetween(OriginLat, OriginLng, s.Latitude, s.Longitude),
            })
            .OrderBy(s => s.DistanceKm)
            .ThenBy(s => s.Id)
            .ToList();

        Assert.Equal(4, expected.Count);
        Assert.Equal(expected.Count, returned.Count);

        Assert.Equal(
            expected.Select(s => s.Name).ToList(),
            // Type argument pinned rather than inferred. `GetString()!` changes the null *flow
            // state* but not the declared type, so Select still infers TResult = string? and the
            // comparison silently becomes List<string> vs List<string?> (CS8620).
            returned.Select<JsonElement, string>(s => s.GetProperty("name").GetString()!).ToList());

        Assert.Equal(
            expected.Select(s => Math.Round(s.DistanceKm, 1, MidpointRounding.AwayFromZero)).ToList(),
            returned.Select(s => s.GetProperty("distanceKm").GetDouble()).ToList());

        // Ascending, restated on the emitted (rounded) values: sorting on the rounded distance
        // would still be ascending here, but a missing OrderBy would not be.
        var distances = returned.Select(s => s.GetProperty("distanceKm").GetDouble()).ToList();
        Assert.Equal(distances.OrderBy(d => d).ToList(), distances);

        var names = returned.Select(s => s.GetProperty("name").GetString()).ToList();
        Assert.DoesNotContain("Tarlac Central Farms", names);
        Assert.DoesNotContain("Benguet Highland Vegetables", names);

        Assert.All(returned, s => Assert.True(s.GetProperty("verified").GetBoolean()));
    }

    /// <summary>
    /// Each supplier carries its own coordinates and its derived crop chips.
    /// </summary>
    /// <remarks>
    /// <c>crops</c> has no column behind it — it is walked from the supplier's listings — so a
    /// broken relation returns an empty array that the frontend renders as "a supplier with no
    /// crops", which is a legitimate state and therefore invisible. Every seeded supplier owns at
    /// least two listings, so a non-empty list is a fair assertion here.
    /// </remarks>
    [Fact]
    public async Task NearbySuppliersResolveTheirLocationAndCrops()
    {
        var (root, _) = await GetOkAsync(
            FormattableString.Invariant($"/api/v1/suppliers/nearby?lat={OriginLat}&lng={OriginLng}"));

        await using var db = _fixture.CreateContext();

        var byName = await db.Suppliers
            .AsNoTracking()
            .ToDictionaryAsync(s => s.Name, s => new { s.Latitude, s.Longitude, s.Region });

        foreach (var supplier in root.GetProperty("suppliers").EnumerateArray())
        {
            var seeded = byName[supplier.GetProperty("name").GetString()!];
            var location = supplier.GetProperty("location");

            Assert.Equal(seeded.Latitude, location.GetProperty("lat").GetDouble());
            Assert.Equal(seeded.Longitude, location.GetProperty("lng").GetDouble());
            Assert.Equal(seeded.Region, supplier.GetProperty("region").GetString());

            var crops = supplier.GetProperty("crops").EnumerateArray().Select(c => c.GetString()).ToList();

            Assert.NotEmpty(crops);

            // Distinct: the derivation walks listings, and a supplier with two rice lots would
            // otherwise emit "rice" twice and render a duplicate chip.
            Assert.Equal(crops.Distinct().Count(), crops.Count);
        }
    }

    [Fact]
    public async Task NearbySuppliersUsesCamelCaseWireNames()
    {
        var (root, raw) = await GetOkAsync(
            FormattableString.Invariant($"/api/v1/suppliers/nearby?lat={OriginLat}&lng={OriginLng}"));

        AssertCamelCaseThroughout(root);

        Assert.Contains("\"distanceKm\"", raw, StringComparison.Ordinal);

        // `lng`, never `lon` or `long`. The frontend's LatLng names it lng, and a mismatch
        // deserialises to 0 rather than failing — every pin ends up off West Africa.
        Assert.Contains("\"lng\"", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("\"lon\"", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("\"longitude\"", raw, StringComparison.Ordinal);
    }

    // ── GET /api/v1/listings/featured ────────────────────────────────────────

    /// <summary>
    /// Omitting <c>limit</c> caps the result at ten and returns every featured lot below that cap.
    /// </summary>
    /// <remarks>
    /// The same <c>[AsParameters]</c> default hazard as <c>months</c>, with a quieter symptom:
    /// binding to 0 makes <c>Take(0)</c> return nothing, and the panel renders its empty state —
    /// which is a designed, correct-looking screen. The cap is asserted alongside the exact count
    /// because the seed has six featured lots, so a broken limit would not be visible from the
    /// count alone.
    /// </remarks>
    [Fact]
    public async Task FeaturedLotsDefaultsToAtMostTenLots()
    {
        var (root, _) = await GetOkAsync("/api/v1/listings/featured");

        var lots = root.GetProperty("lots");

        await using var db = _fixture.CreateContext();
        var featuredCount = await db.Listings.CountAsync(l => l.IsFeatured);

        Assert.InRange(lots.GetArrayLength(), 1, 10);
        Assert.Equal(Math.Min(featuredCount, 10), lots.GetArrayLength());
    }

    [Fact]
    public async Task FeaturedLotsHonoursAnExplicitLimit()
    {
        var (root, _) = await GetOkAsync("/api/v1/listings/featured?limit=3");

        Assert.Equal(3, root.GetProperty("lots").GetArrayLength());
    }

    /// <summary>
    /// Each lot's fields match the row behind it, including the two renames.
    /// </summary>
    /// <remarks>
    /// Three transcriptions are checked because each fails silently:
    /// <c>MinimumOrderKg</c> → <c>minOrderKg</c> (a name mismatch renders "Min. order: —"),
    /// <c>Crop.Name</c> → <c>crop</c> rather than the lot's trade name (the frontend looks up a
    /// colour and a translation by this key, so "Dinorado Rice" fails as a missing i18n key), and
    /// the id as a string rather than a number (the frontend compares ids with <c>===</c>).
    /// </remarks>
    [Fact]
    public async Task FeaturedLotsFlattenSupplierCropAndPricingFromTheSeededRow()
    {
        var (root, _) = await GetOkAsync("/api/v1/listings/featured");

        await using var db = _fixture.CreateContext();

        var seeded = await db.Listings
            .AsNoTracking()
            .Where(l => l.IsFeatured)
            .Select(l => new
            {
                Id = l.Id.ToString(),
                l.Name,
                Crop = l.Crop!.Name,
                l.Grade,
                Supplier = l.Supplier!.Name,
                l.Supplier.Region,
                l.Verified,
                l.VolumeKg,
                l.MinimumOrderKg,
                l.PricePerKg,
                l.CreatedAt,
            })
            .ToListAsync();

        var lots = root.GetProperty("lots").EnumerateArray().ToList();

        // Newest first — the panel is "Featured Wholesale Lots", and a stale lot at the top is
        // the difference between a curated shelf and an arbitrary one.
        Assert.Equal(
            seeded.OrderByDescending(l => l.CreatedAt).Select(l => l.Id).ToList(),
            lots.Select<JsonElement, string>(l => l.GetProperty("id").GetString()!).ToList());

        foreach (var lot in lots)
        {
            var row = seeded.Single(l => l.Id == lot.GetProperty("id").GetString());

            Assert.Equal(row.Name, lot.GetProperty("name").GetString());
            Assert.Equal(row.Crop, lot.GetProperty("crop").GetString());
            Assert.Equal(row.Grade, lot.GetProperty("grade").GetString());
            Assert.Equal(row.Supplier, lot.GetProperty("supplier").GetString());
            Assert.Equal(row.Region, lot.GetProperty("region").GetString());
            Assert.Equal(row.Verified, lot.GetProperty("verified").GetBoolean());
            Assert.Equal(row.VolumeKg, lot.GetProperty("volumeKg").GetInt32());
            Assert.Equal(row.MinimumOrderKg, lot.GetProperty("minOrderKg").GetInt32());
            Assert.Equal(row.PricePerKg, lot.GetProperty("pricePerKg").GetDecimal());

            // The crop key is one of the three lowercase SeriesKey values, never the trade name.
            Assert.Contains(lot.GetProperty("crop").GetString(), new[] { "rice", "corn", "vegetables" });

            // A string id, not a number. JsonValueKind is the only way to see the difference —
            // GetString() on a number throws, but a lax assertion would never call it.
            Assert.Equal(JsonValueKind.String, lot.GetProperty("id").ValueKind);
        }
    }

    [Fact]
    public async Task FeaturedLotsUseCamelCaseWireNames()
    {
        var (root, raw) = await GetOkAsync("/api/v1/listings/featured");

        AssertCamelCaseThroughout(root);

        Assert.Contains("\"pricePerKg\"", raw, StringComparison.Ordinal);
        Assert.Contains("\"minOrderKg\"", raw, StringComparison.Ordinal);

        // The entity's name must not leak: the frontend's Lot has no `minimumOrderKg`.
        Assert.DoesNotContain("\"minimumOrderKg\"", raw, StringComparison.Ordinal);
    }

    // ── GET /api/v1/orders/recent ────────────────────────────────────────────

    /// <summary>
    /// <b>Status is lowercase on the wire.</b>
    /// </summary>
    /// <remarks>
    /// The single highest-value assertion in this file. <c>Order.Status</c> is an enum persisted
    /// by name, so the database holds <c>"Confirmed"</c>; the frontend's <c>StatusKey</c> union is
    /// lowercase and is used verbatim as a lookup into both the badge colour map and the i18n
    /// bundle. Emitting the stored casing produces a badge with no colour and a label with no
    /// translation — no exception, no log line, no failing type check, because JSON carries a
    /// string either way.
    /// <para>
    /// Asserted three ways on purpose: that every value is in the known lowercase set (a typo
    /// would pass a bare <c>ToLower()</c> check), that the raw body contains no capitalised
    /// variant (a value reaching the wire through some other path is still caught), and that the
    /// values round-trip to a real <see cref="OrderStatus"/> (so a lowercasing that mangled a name
    /// rather than merely recasing it fails here).
    /// </para>
    /// </remarks>
    [Fact]
    public async Task RecentOrdersEmitStatusInLowercase()
    {
        var (root, raw) = await GetOkAsync("/api/v1/orders/recent");

        var statuses = root.GetProperty("orders")
            .EnumerateArray()
            .Select(o => o.GetProperty("status").GetString()!)
            .ToList();

        Assert.NotEmpty(statuses);

        foreach (var status in statuses)
        {
            Assert.Contains(status, new[] { "confirmed", "processing", "shipped", "delivered" });
            Assert.Equal(status.ToLowerInvariant(), status);
            Assert.True(
                Enum.TryParse<OrderStatus>(status, ignoreCase: true, out _),
                $"'{status}' is not an OrderStatus name; the mapper mangled it rather than recasing it.");
        }

        // The seed covers all four statuses twice over, so every badge is exercised.
        Assert.Equal(4, statuses.Distinct().Count());

        foreach (var wrong in new[] { "Confirmed", "Processing", "Shipped", "Delivered" })
        {
            Assert.DoesNotContain($"\"{wrong}\"", raw, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// <c>id</c> is the human order reference, not the surrogate key.
    /// </summary>
    /// <remarks>
    /// The table prints this cell directly and a buyer quoting "7" to support helps nobody. The
    /// bare-integer check is the one that matters: <c>Order.Id</c> stringified is a perfectly
    /// valid-looking id that would only be noticed by whoever reads the deployed table.
    /// </remarks>
    [Fact]
    public async Task RecentOrderIdsAreHumanReferencesNotSurrogateKeys()
    {
        var (root, _) = await GetOkAsync("/api/v1/orders/recent");

        await using var db = _fixture.CreateContext();
        var references = await db.Orders.AsNoTracking().Select(o => o.Reference).ToListAsync();

        foreach (var order in root.GetProperty("orders").EnumerateArray())
        {
            var id = order.GetProperty("id");

            Assert.Equal(JsonValueKind.String, id.ValueKind);

            var value = id.GetString()!;

            Assert.Matches(@"^AK-\d+$", value);
            Assert.False(int.TryParse(value, out _), $"'{value}' is a bare integer, not a reference.");
            Assert.Contains(value, references);
        }
    }

    /// <summary>
    /// Newest first, and the row contents come from two joins the orders table cannot answer alone.
    /// </summary>
    /// <remarks>
    /// "Recent" is about when the order was <i>placed</i>, not when it is due to arrive — sorting
    /// by <c>estimatedDelivery</c> would produce a differently-ordered but entirely plausible
    /// table, which is why the expected sequence is recomputed from <c>CreatedAt</c> here. Product
    /// and supplier names come from order → listing → supplier; a dropped join surfaces as an
    /// empty cell rather than an error.
    /// </remarks>
    [Fact]
    public async Task RecentOrdersAreNewestFirstWithResolvedNames()
    {
        var (root, _) = await GetOkAsync("/api/v1/orders/recent");

        await using var db = _fixture.CreateContext();

        var seeded = await db.Orders
            .AsNoTracking()
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new
            {
                o.Reference,
                Product = o.Listing!.Name,
                Supplier = o.Listing.Supplier!.Name,
                o.QuantityKg,
                o.EstimatedDelivery,
            })
            .ToListAsync();

        var orders = root.GetProperty("orders").EnumerateArray().ToList();

        Assert.Equal(seeded.Count, orders.Count);

        Assert.Equal(
            seeded.Select(o => o.Reference).ToList(),
            orders.Select<JsonElement, string>(o => o.GetProperty("id").GetString()!).ToList());

        for (var i = 0; i < orders.Count; i++)
        {
            Assert.Equal(seeded[i].Product, orders[i].GetProperty("product").GetString());
            Assert.Equal(seeded[i].Supplier, orders[i].GetProperty("supplier").GetString());
            Assert.Equal(seeded[i].QuantityKg, orders[i].GetProperty("quantityKg").GetInt32());

            // A bare ISO day, not a timestamp. See the trends assertion for why the time zone
            // matters more than the extra characters.
            Assert.Equal(
                seeded[i].EstimatedDelivery.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                orders[i].GetProperty("estimatedDelivery").GetString());
        }
    }

    [Fact]
    public async Task RecentOrdersHonourAnExplicitLimit()
    {
        var (root, _) = await GetOkAsync("/api/v1/orders/recent?limit=3");

        Assert.Equal(3, root.GetProperty("orders").GetArrayLength());
    }

    [Fact]
    public async Task RecentOrdersUseCamelCaseWireNames()
    {
        var (root, raw) = await GetOkAsync("/api/v1/orders/recent");

        AssertCamelCaseThroughout(root);

        Assert.Contains("\"estimatedDelivery\"", raw, StringComparison.Ordinal);
        Assert.Contains("\"quantityKg\"", raw, StringComparison.Ordinal);

        // `product`, not `listing` or `listingName` — the frontend's Order names it that way.
        Assert.Contains("\"product\"", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("\"reference\"", raw, StringComparison.Ordinal);
    }

    // ── Cross-cutting ────────────────────────────────────────────────────────

    /// <summary>
    /// All five routes exist under <c>/api/v1</c> and return a non-empty payload.
    /// </summary>
    /// <remarks>
    /// Cheap insurance against the one regression the per-endpoint tests above cannot express:
    /// a route added to <c>MapDashboardEndpoints</c> and later removed, or a group prefix changed
    /// from <c>/api/v1</c>. Both are 404s on a deployed frontend, and neither breaks a service
    /// test. <c>DashboardEndpoints.BasePath</c> is deliberately *not* referenced here: a test that
    /// builds its URLs from the same constant the routes are built from would follow a prefix
    /// change silently, which is precisely the regression this is meant to catch.
    /// </remarks>
    [Theory]
    [InlineData("/api/v1/buyer/overview/stats")]
    [InlineData("/api/v1/pricing/trends")]
    [InlineData("/api/v1/suppliers/nearby?lat=14.5995&lng=120.9842")]
    [InlineData("/api/v1/listings/featured")]
    [InlineData("/api/v1/orders/recent")]
    public async Task EveryDashboardRouteRespondsUnderTheVersionedPrefix(string url)
    {
        var (root, raw) = await GetOkAsync(url);

        Assert.Equal(JsonValueKind.Object, root.ValueKind);

        // One wrapper property holding a non-empty array. An object wrapper rather than a bare
        // array is itself contract: it leaves room to add pagination or an as-of timestamp
        // without breaking every consumer.
        // Materialised before Assert.Single because JsonElement.ObjectEnumerator is a struct
        // enumerator, which the xUnit analyser cannot see through — it reads the call as a
        // filter-then-Single and raises xUnit2031. The assertion is the same either way.
        var properties = root.EnumerateObject().ToList();
        var payload = Assert.Single(properties, p => p.Value.ValueKind == JsonValueKind.Array);

        Assert.True(payload.Value.GetArrayLength() > 0, $"GET {url} returned an empty '{payload.Name}': {raw}");
    }
}
