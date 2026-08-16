using AniKo_API.Data;
using AniKo_API.Data.Seed;
using AniKo_API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AniKo_API.Tests;

/// <summary>
/// Asserts the shape of the EF model, not the behaviour of a database.
/// <para>
/// <c>UseNpgsql</c> with a dummy connection string never opens a socket — building the model is
/// entirely offline — so these run in CI with no Postgres and still catch the mistakes that only
/// show up in Postgres: a price silently mapped to <c>double precision</c>, a timestamp mapped
/// without a time zone, an index that was renamed out of existence by a refactor.
/// </para>
/// </summary>
public class AniKoDbContextModelTests
{
    private const string MoneyColumnType = "numeric(18,2)";
    private const string TimestampColumnType = "timestamp with time zone";

    /// <summary>
    /// The credentials are deliberate nonsense. Npgsql parses the string when the context is
    /// constructed and connects only on first query, and nothing here queries.
    /// </summary>
    private static AniKoDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AniKoDbContext>()
            .UseNpgsql("Host=localhost;Database=x;Username=x;Password=x")
            .Options;

        return new AniKoDbContext(options);
    }

    /// <summary>
    /// <c>HasData</c> rows are stripped from <c>context.Model</c>, which is the runtime
    /// read-optimised model; asking it for seed data throws rather than returning nothing.
    /// The design-time model is the one migrations are generated from, so it is also the
    /// honest thing to assert against here.
    /// </summary>
    private static IModel DesignTimeModel(AniKoDbContext context) =>
        context.GetService<IDesignTimeModel>().Model;

    private static IProperty Property<TEntity>(AniKoDbContext context, string name)
        where TEntity : class
    {
        var entityType = context.Model.FindEntityType(typeof(TEntity));
        Assert.NotNull(entityType);

        var property = entityType.FindProperty(name);
        Assert.NotNull(property);

        return property;
    }

    // ── Money ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(typeof(Listing), nameof(Listing.PricePerKg))]
    [InlineData(typeof(PriceObservation), nameof(PriceObservation.PricePerKg))]
    public void MoneyColumnsAreFixedPointNumeric(Type entityClrType, string propertyName)
    {
        using var context = CreateContext();

        var property = context.Model.FindEntityType(entityClrType)!.FindProperty(propertyName)!;

        Assert.Equal(typeof(decimal), property.ClrType);
        Assert.Equal(MoneyColumnType, property.GetColumnType());
    }

    /// <summary>
    /// The rule is "no floats anywhere near a price", and the way that rule gets broken is by a
    /// later entity, not by these two. This sweeps the whole model so a new money column added
    /// as <c>double</c> fails here rather than in a rounding complaint months later.
    /// </summary>
    [Fact]
    public void NoFloatingPointPropertyIsNamedLikeMoney()
    {
        using var context = CreateContext();

        var offenders = context.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p => p.Name.Contains("Price", StringComparison.Ordinal)
                        || p.Name.Contains("Amount", StringComparison.Ordinal)
                        || p.Name.Contains("Total", StringComparison.Ordinal))
            .Where(p => p.ClrType == typeof(double) || p.ClrType == typeof(float))
            .Select(p => $"{p.DeclaringType.ClrType.Name}.{p.Name}")
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// Latitude and longitude are the one place <c>double</c> is correct, and they are close
    /// enough to the money rule to be worth pinning so nobody "fixes" them into decimals.
    /// </summary>
    [Fact]
    public void CoordinatesAreDoubleBecauseTheyAreMeasurementsNotMoney()
    {
        using var context = CreateContext();

        Assert.Equal(typeof(double), Property<Supplier>(context, nameof(Supplier.Latitude)).ClrType);
        Assert.Equal(typeof(double), Property<Supplier>(context, nameof(Supplier.Longitude)).ClrType);
    }

    // ── Time ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(typeof(AppUser), nameof(AppUser.CreatedAt))]
    [InlineData(typeof(Listing), nameof(Listing.CreatedAt))]
    [InlineData(typeof(Order), nameof(Order.CreatedAt))]
    [InlineData(typeof(SeedHistory), nameof(SeedHistory.AppliedAt))]
    public void TimestampsCarryATimeZone(Type entityClrType, string propertyName)
    {
        using var context = CreateContext();

        var property = context.Model.FindEntityType(entityClrType)!.FindProperty(propertyName)!;

        Assert.Equal(TimestampColumnType, property.GetColumnType());
    }

    /// <summary>
    /// Same sweep as the money one: catches a future <c>DateTime</c> that nobody remembered to
    /// configure and which Npgsql would otherwise map to a naive <c>timestamp</c>.
    /// </summary>
    [Fact]
    public void EveryDateTimePropertyIsTimestamptz()
    {
        using var context = CreateContext();

        var offenders = context.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?))
            .Where(p => p.GetColumnType() != TimestampColumnType)
            .Select(p => $"{p.DeclaringType.ClrType.Name}.{p.Name} -> {p.GetColumnType()}")
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// Delivery dates and price months are calendar dates. Storing either as a timestamp invents
    /// a midnight that then moves under a timezone conversion.
    /// </summary>
    [Theory]
    [InlineData(typeof(Order), nameof(Order.EstimatedDelivery))]
    [InlineData(typeof(PriceObservation), nameof(PriceObservation.Month))]
    public void CalendarDatesAreDateNotTimestamp(Type entityClrType, string propertyName)
    {
        using var context = CreateContext();

        var property = context.Model.FindEntityType(entityClrType)!.FindProperty(propertyName)!;

        Assert.Equal(typeof(DateOnly), property.ClrType);
        Assert.Equal("date", property.GetColumnType());
    }

    // ── Enums ────────────────────────────────────────────────────────────────

    /// <summary>
    /// The decision under test is "persist the name, not the ordinal". If this ever fails
    /// because someone dropped the conversion, every existing row's meaning would depend on the
    /// current declaration order of the enum — which is exactly the silent breakage the string
    /// mapping exists to prevent.
    /// </summary>
    [Fact]
    public void RoleIsPersistedAsItsName()
    {
        using var context = CreateContext();

        var property = Property<AppUser>(context, nameof(AppUser.Role));

        Assert.Equal(typeof(string), property.GetProviderClrType() ?? property.ClrType);
        Assert.Equal(16, property.GetMaxLength());
    }

    [Fact]
    public void StatusIsPersistedAsItsName()
    {
        using var context = CreateContext();

        var property = Property<Order>(context, nameof(Order.Status));

        Assert.Equal(typeof(string), property.GetProviderClrType() ?? property.ClrType);
        Assert.Equal(16, property.GetMaxLength());
    }

    /// <summary>
    /// The frontend has badge colours for exactly four statuses and types its orders against
    /// that key set, so a fifth added here would render as an unstyled badge rather than fail.
    /// This is the backend half of that contract.
    /// </summary>
    [Fact]
    public void OrderStatusSetIsExactlyTheFourTheFrontendCanRender()
    {
        Assert.Equal(
            new[] { "Confirmed", "Processing", "Shipped", "Delivered" },
            Enum.GetNames<OrderStatus>());
    }

    [Fact]
    public void UserRoleSetIsExactlyBuyerAndFarmer()
    {
        Assert.Equal(new[] { "Buyer", "Farmer" }, Enum.GetNames<UserRole>());
    }

    // ── Indexes ──────────────────────────────────────────────────────────────

    private static bool HasIndexOn<TEntity>(AniKoDbContext context, params string[] propertyNames)
        where TEntity : class
    {
        var entityType = context.Model.FindEntityType(typeof(TEntity));
        Assert.NotNull(entityType);

        return entityType.GetIndexes()
            .Any(i => i.Properties.Select(p => p.Name).SequenceEqual(propertyNames));
    }

    [Fact]
    public void FeaturedListingsAreIndexed()
    {
        using var context = CreateContext();

        Assert.True(
            HasIndexOn<Listing>(context, nameof(Listing.IsFeatured)),
            "GET /listings/featured filters on IsFeatured on every call.");
    }

    [Fact]
    public void RecentOrdersAreIndexedByPlacementTime()
    {
        using var context = CreateContext();

        Assert.True(
            HasIndexOn<Order>(context, nameof(Order.CreatedAt)),
            "GET /orders/recent sorts by CreatedAt.");
    }

    /// <summary>
    /// Order matters: <c>CropId</c> is the equality predicate and <c>Month</c> the range, so a
    /// (Month, CropId) index would still exist and still be near-useless for this query. The
    /// assertion is on the sequence, not on membership.
    /// </summary>
    [Fact]
    public void PriceTrendsAreIndexedByCropThenMonth()
    {
        using var context = CreateContext();

        Assert.True(
            HasIndexOn<PriceObservation>(
                context,
                nameof(PriceObservation.CropId),
                nameof(PriceObservation.Month)),
            "GET /pricing/trends filters by crop and scans a month range.");
    }

    [Fact]
    public void OrderReferenceIsUnique()
    {
        using var context = CreateContext();

        var index = context.Model.FindEntityType(typeof(Order))!.GetIndexes()
            .SingleOrDefault(i => i.Properties.Select(p => p.Name)
                .SequenceEqual(new[] { nameof(Order.Reference) }));

        Assert.NotNull(index);
        Assert.True(index.IsUnique);
    }

    [Fact]
    public void SeedHistoryVersionIsUniqueSoAConcurrentSeedCannotDoubleTheData()
    {
        using var context = CreateContext();

        var index = context.Model.FindEntityType(typeof(SeedHistory))!.GetIndexes()
            .SingleOrDefault(i => i.Properties.Select(p => p.Name)
                .SequenceEqual(new[] { nameof(SeedHistory.Version) }));

        Assert.NotNull(index);
        Assert.True(index.IsUnique);
    }

    // ── Required / max length ────────────────────────────────────────────────

    [Theory]
    [InlineData(typeof(AppUser), nameof(AppUser.Name), 128)]
    [InlineData(typeof(Crop), nameof(Crop.Name), 64)]
    [InlineData(typeof(Crop), nameof(Crop.Unit), 8)]
    [InlineData(typeof(Supplier), nameof(Supplier.Name), 128)]
    [InlineData(typeof(Supplier), nameof(Supplier.Region), 128)]
    [InlineData(typeof(Listing), nameof(Listing.Name), 128)]
    [InlineData(typeof(Listing), nameof(Listing.Grade), 8)]
    [InlineData(typeof(Order), nameof(Order.Reference), 32)]
    [InlineData(typeof(PriceObservation), nameof(PriceObservation.Region), 128)]
    [InlineData(typeof(SeedHistory), nameof(SeedHistory.Version), 64)]
    public void RequiredTextColumnsAreBoundedAndNotNullable(
        Type entityClrType,
        string propertyName,
        int maxLength)
    {
        using var context = CreateContext();

        var property = context.Model.FindEntityType(entityClrType)!.FindProperty(propertyName)!;

        Assert.False(property.IsNullable);
        Assert.Equal(maxLength, property.GetMaxLength());
    }

    /// <summary>
    /// These three are genuinely optional — the UI has a fallback for each — so "nullable" is
    /// the assertion, not an oversight. An unbounded URL column is still a denial-of-service
    /// surface, hence the length.
    /// </summary>
    [Theory]
    [InlineData(typeof(AppUser), nameof(AppUser.AvatarUrl))]
    [InlineData(typeof(Supplier), nameof(Supplier.ThumbnailUrl))]
    [InlineData(typeof(Listing), nameof(Listing.PhotoUrl))]
    public void OptionalImageUrlsAreNullableAndBounded(Type entityClrType, string propertyName)
    {
        using var context = CreateContext();

        var property = context.Model.FindEntityType(entityClrType)!.FindProperty(propertyName)!;

        Assert.True(property.IsNullable);
        Assert.Equal(512, property.GetMaxLength());
    }

    [Fact]
    public void EveryEntityHasAnExplicitPrimaryKey()
    {
        using var context = CreateContext();

        var withoutKey = context.Model.GetEntityTypes()
            .Where(e => e.FindPrimaryKey() is null)
            .Select(e => e.ClrType.Name)
            .ToList();

        Assert.Empty(withoutKey);
    }

    // ── Reference data ───────────────────────────────────────────────────────

    /// <summary>
    /// The crop names must be the frontend's lowercase series keys: the chart looks up a colour
    /// by this exact string and the i18n layer looks up <c>crop.&lt;name&gt;</c>. A display-cased
    /// "Rice" would produce an uncoloured series and a missing translation, neither of which is
    /// a build failure on either side.
    /// </summary>
    [Fact]
    public void CropReferenceDataIsSeededWithPinnedIdsAndFrontendKeys()
    {
        using var context = CreateContext();

        var seeded = DesignTimeModel(context).FindEntityType(typeof(Crop))!.GetSeedData()
            .Select(row => ((int)row[nameof(Crop.Id)]!, (string)row[nameof(Crop.Name)]!, (string)row[nameof(Crop.Unit)]!))
            .OrderBy(row => row.Item1)
            .ToList();

        Assert.Equal(
            [(1, "rice", "kg"), (2, "corn", "kg"), (3, "vegetables", "kg")],
            seeded);
    }

    /// <summary>
    /// Guards the one thing that must not go into <c>HasData</c>. Demo data there would rewrite a
    /// migration on every edit to the dataset.
    /// </summary>
    [Fact]
    public void OnlyCropsAreSeededThroughHasData()
    {
        using var context = CreateContext();

        var seededEntities = DesignTimeModel(context).GetEntityTypes()
            .Where(e => e.GetSeedData().Any())
            .Select(e => e.ClrType.Name)
            .ToList();

        Assert.Equal([nameof(Crop)], seededEntities);
    }

    /// <summary>
    /// The regions list is the seeder's only source of locality strings, so a duplicate or a
    /// stray whitespace variant here becomes two "different" regions in the price trends.
    /// </summary>
    [Fact]
    public void ReferenceRegionsAreDistinctAndNonEmpty()
    {
        Assert.NotEmpty(ReferenceData.Regions);
        Assert.All(ReferenceData.Regions, r => Assert.False(string.IsNullOrWhiteSpace(r)));
        Assert.Equal(ReferenceData.Regions.Count, ReferenceData.Regions.Distinct().Count());
        Assert.All(ReferenceData.Regions, r => Assert.Equal(r.Trim(), r));
    }

    /// <summary>
    /// Regions are strings on the entities and constants here — no <c>Region</c> entity. Pinned
    /// so the decision is revisited deliberately rather than drifted into.
    /// </summary>
    [Fact]
    public void RegionIsAStringColumnAndNotAForeignKey()
    {
        using var context = CreateContext();

        Assert.Equal(typeof(string), Property<Supplier>(context, nameof(Supplier.Region)).ClrType);
        Assert.Equal(typeof(string), Property<PriceObservation>(context, nameof(PriceObservation.Region)).ClrType);

        Assert.DoesNotContain(
            context.Model.GetEntityTypes(),
            e => e.ClrType.Name == "Region");
    }
}
