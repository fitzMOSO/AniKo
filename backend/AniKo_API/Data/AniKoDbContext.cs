using AniKo_API.Data.Seed;
using AniKo_API.Models;
using Microsoft.EntityFrameworkCore;

namespace AniKo_API.Data;

/// <summary>
/// The one EF Core context. Everything is configured explicitly in
/// <see cref="OnModelCreating"/> — no reliance on convention.
/// <para>
/// The reason is narrow and worth stating: conventions are provider- and version-dependent, and
/// the failure they produce is a column type that is subtly wrong in production and correct on a
/// laptop. A <c>decimal</c> left to convention is the concrete case — Npgsql defaults it to
/// <c>numeric</c> with no precision, which stores prices fine right up until something rounds.
/// Writing the types out means <c>AniKoDbContextModelTests</c> can assert on them.
/// </para>
/// </summary>
public class AniKoDbContext : DbContext
{
    /// <summary>
    /// Money, everywhere, without exception. PHP only — there is no currency column, because a
    /// second currency is a schema change and a nullable currency column would be a worse
    /// pretence that it is not.
    /// </summary>
    private const string MoneyColumnType = "numeric(18,2)";

    /// <summary>
    /// Every instant is stored with its offset and written as UTC. <c>timestamp</c> without a
    /// time zone is the trap: it round-trips correctly on a machine set to UTC and silently
    /// shifts every dashboard figure by eight hours on one set to Asia/Manila.
    /// </summary>
    private const string TimestampColumnType = "timestamp with time zone";

    public AniKoDbContext(DbContextOptions<AniKoDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppUser> AppUsers => Set<AppUser>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<Crop> Crops => Set<Crop>();

    public DbSet<Listing> Listings => Set<Listing>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<PriceObservation> PriceObservations => Set<PriceObservation>();

    public DbSet<SeedHistory> SeedHistory => Set<SeedHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureAppUser(modelBuilder);
        ConfigureCrop(modelBuilder);
        ConfigureSupplier(modelBuilder);
        ConfigureListing(modelBuilder);
        ConfigureOrder(modelBuilder);
        ConfigurePriceObservation(modelBuilder);
        ConfigureSeedHistory(modelBuilder);
    }

    private static void ConfigureAppUser(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("app_users");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name).IsRequired().HasMaxLength(128);

            // Stored as "Buyer"/"Farmer", not 0/1. Two reasons, both about failure modes.
            // An int survives nobody: someone reading the table in psql to answer "why is this
            // dashboard empty" sees a 1 and has to go find the enum. And reordering the enum —
            // an edit that looks harmless in a diff — silently reinterprets every existing row,
            // with no migration and no error. A string breaks loudly instead, on a rename.
            entity.Property(e => e.Role)
                .IsRequired()
                .HasMaxLength(16)
                .HasConversion<string>();

            entity.Property(e => e.Verified).IsRequired();
            entity.Property(e => e.AvatarUrl).HasMaxLength(512);
            entity.Property(e => e.CreatedAt).IsRequired().HasColumnType(TimestampColumnType);
        });
    }

    private static void ConfigureCrop(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Crop>(entity =>
        {
            entity.ToTable("crops");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Unit).IsRequired().HasMaxLength(8);

            // The chart looks up a colour and a translation by this name, so two crops sharing
            // one would collapse two series into one line rather than error anywhere visible.
            entity.HasIndex(e => e.Name).IsUnique();

            // Reference data, baked into the migration. Ids are pinned in ReferenceData because
            // seeded listings and price observations carry them.
            entity.HasData(ReferenceData.Crops);
        });
    }

    private static void ConfigureSupplier(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.ToTable("suppliers");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Region).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Latitude).IsRequired();
            entity.Property(e => e.Longitude).IsRequired();
            entity.Property(e => e.Verified).IsRequired();
            entity.Property(e => e.ThumbnailUrl).HasMaxLength(512);

            entity.HasOne(e => e.AppUser)
                .WithMany()
                .HasForeignKey(e => e.AppUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureListing(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Listing>(entity =>
        {
            entity.ToTable("listings");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Grade).IsRequired().HasMaxLength(8);
            entity.Property(e => e.VolumeKg).IsRequired();
            entity.Property(e => e.PricePerKg).IsRequired().HasColumnType(MoneyColumnType);
            entity.Property(e => e.MinimumOrderKg).IsRequired();
            entity.Property(e => e.PhotoUrl).HasMaxLength(512);
            entity.Property(e => e.Verified).IsRequired();
            entity.Property(e => e.IsFeatured).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired().HasColumnType(TimestampColumnType);

            entity.HasOne(e => e.Supplier)
                .WithMany()
                .HasForeignKey(e => e.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            // Restrict, not Cascade: a crop is reference data, and deleting one should fail
            // rather than take every listing that mentions it with it.
            entity.HasOne(e => e.Crop)
                .WithMany()
                .HasForeignKey(e => e.CropId)
                .OnDelete(DeleteBehavior.Restrict);

            // GET /api/v1/listings/featured filters on this on every call.
            entity.HasIndex(e => e.IsFeatured);
        });
    }

    private static void ConfigureOrder(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Reference).IsRequired().HasMaxLength(32);
            entity.Property(e => e.QuantityKg).IsRequired();

            // Stored as "Confirmed"/"Processing"/"Shipped"/"Delivered". Same reasoning as
            // AppUser.Role, with one extra edge: these four values are also the frontend's badge
            // keys, so a string column and the rendered badge set are the same vocabulary and a
            // mismatch is greppable across both repos halves.
            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(16)
                .HasConversion<string>();

            // date, not timestamptz — see the property comment.
            entity.Property(e => e.EstimatedDelivery).IsRequired().HasColumnType("date");
            entity.Property(e => e.CreatedAt).IsRequired().HasColumnType(TimestampColumnType);

            entity.HasOne(e => e.Buyer)
                .WithMany()
                .HasForeignKey(e => e.BuyerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Listing)
                .WithMany()
                .HasForeignKey(e => e.ListingId)
                .OnDelete(DeleteBehavior.Restrict);

            // The human reference is quoted in support conversations; two orders sharing one
            // makes that conversation impossible to resolve.
            entity.HasIndex(e => e.Reference).IsUnique();

            // GET /api/v1/orders/recent sorts by this. "Recent" is about placement, not delivery.
            entity.HasIndex(e => e.CreatedAt);
        });
    }

    private static void ConfigurePriceObservation(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PriceObservation>(entity =>
        {
            entity.ToTable("price_observations");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Region).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Month).IsRequired().HasColumnType("date");
            entity.Property(e => e.PricePerKg).IsRequired().HasColumnType(MoneyColumnType);

            entity.HasOne(e => e.Crop)
                .WithMany()
                .HasForeignKey(e => e.CropId)
                .OnDelete(DeleteBehavior.Restrict);

            // GET /api/v1/pricing/trends?months= is a per-crop scan over a month range, so the
            // composite is ordered crop-then-month: the equality column has to come first for
            // the range on the second to be usable.
            entity.HasIndex(e => new { e.CropId, e.Month });
        });
    }

    private static void ConfigureSeedHistory(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SeedHistory>(entity =>
        {
            entity.ToTable("seed_history");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Version).IsRequired().HasMaxLength(64);
            entity.Property(e => e.AppliedAt).IsRequired().HasColumnType(TimestampColumnType);

            entity.HasIndex(e => e.Version).IsUnique();
        });
    }
}
