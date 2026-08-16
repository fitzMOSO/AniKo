using AniKo_API.Dtos;
using AniKo_API.Repositories;
using AniKo_API.Services;

namespace AniKo_API.Tests.Services;

public class NearbySupplierServiceTests
{
    /// <summary>Manila, the buyer's position in the demo data.</summary>
    private static readonly LatLngDto Origin = new(14.5995, 120.9842);

    private static NearbySupplierService Build(params SupplierWithCrops[] rows) =>
        new(new FakeSupplierRepository { Rows = rows });

    /// <summary>
    /// Given deliberately out of distance order, so a service that simply passes the repository's
    /// order through cannot pass.
    /// </summary>
    private static readonly SupplierWithCrops[] ThreeSuppliers =
    [
        Rows.VerifiedSupplier(3, "Cebu Traders", 10.3157, 123.8854, "vegetables"),
        Rows.VerifiedSupplier(1, "Bataan Rice Growers", 14.6761, 120.5363, "rice"),
        Rows.VerifiedSupplier(2, "Nueva Ecija Grain Cooperative", 15.4864, 120.9675, "rice", "corn"),
    ];

    [Fact]
    public async Task GetAsync_OrdersAscendingByDistanceFromTheOrigin()
    {
        var result = await Build(ThreeSuppliers).GetAsync(Origin, limit: 10);

        Assert.Equal(
            ["Bataan Rice Growers", "Nueva Ecija Grain Cooperative", "Cebu Traders"],
            [.. result.Suppliers.Select(s => s.Name)]);

        Assert.Equal(
            result.Suppliers.Select(s => s.DistanceKm).Order(),
            result.Suppliers.Select(s => s.DistanceKm));
    }

    [Fact]
    public async Task GetAsync_TakesTheNearestLimitSuppliersNotTheFirstLimitRows()
    {
        var result = await Build(ThreeSuppliers).GetAsync(Origin, limit: 2);

        // Cebu is first out of the repository and last by distance, so a service that took before
        // it sorted would return it here.
        Assert.Equal(
            ["Bataan Rice Growers", "Nueva Ecija Grain Cooperative"],
            [.. result.Suppliers.Select(s => s.Name)]);
    }

    /// <summary>
    /// <b>Ties break deterministically, by id.</b>
    /// </summary>
    /// <remarks>
    /// Two suppliers in the same town — or simply two whose distances round to the same figure —
    /// leave the sort free to emit them in whatever order the repository produced, and Postgres
    /// does not promise a stable order for rows with equal sort keys. The visible symptom is a
    /// supplier list that reshuffles between two identical requests, which gets reported as a
    /// flickering UI rather than as a missing ORDER BY. The ids here are supplied descending so
    /// that "unchanged input order" and "ascending by id" cannot both be true.
    /// </remarks>
    [Fact]
    public async Task GetAsync_SuppliersAtIdenticalDistances_AreOrderedByIdAndDoNotReshuffle()
    {
        SupplierWithCrops[] coincident =
        [
            Rows.VerifiedSupplier(9, "Third", 14.0, 121.0),
            Rows.VerifiedSupplier(5, "First", 14.0, 121.0),
            Rows.VerifiedSupplier(7, "Second", 14.0, 121.0),
        ];

        var service = Build(coincident);

        var first = await service.GetAsync(Origin, limit: 10);
        var second = await service.GetAsync(Origin, limit: 10);

        Assert.Equal(["5", "7", "9"], [.. first.Suppliers.Select(s => s.Id)]);
        Assert.Equal(
            first.Suppliers.Select(s => s.Id),
            second.Suppliers.Select(s => s.Id));
    }

    /// <summary>
    /// A supplier standing exactly on the origin is 0 km away, not NaN — the coincident-points
    /// case from <see cref="GeoDistanceTests"/>, exercised through the service that would
    /// actually be handed one.
    /// </summary>
    [Fact]
    public async Task GetAsync_SupplierAtTheOrigin_IsZeroKilometresAndSortsFirst()
    {
        SupplierWithCrops[] rows =
        [
            Rows.VerifiedSupplier(2, "Far", 10.3157, 123.8854),
            Rows.VerifiedSupplier(1, "Here", Origin.Lat, Origin.Lng),
        ];

        var result = await Build(rows).GetAsync(Origin, limit: 10);

        Assert.Equal("Here", result.Suppliers[0].Name);
        Assert.Equal(0.0, result.Suppliers[0].DistanceKm);
    }

    [Fact]
    public async Task GetAsync_NoVerifiedSuppliers_YieldsAnEmptyListAndStillEchoesTheOrigin()
    {
        var result = await Build().GetAsync(Origin, limit: 10);

        Assert.Empty(result.Suppliers);
        Assert.Equal(Origin, result.Origin);
    }

    /// <summary>
    /// The origin comes back on the response because the frontend renders "within N km of X" and
    /// centres the map on it; re-deriving it client-side is how the list and the map come to
    /// disagree about where the buyer is.
    /// </summary>
    [Fact]
    public async Task GetAsync_EchoesTheOriginBackUnchanged()
    {
        var result = await Build(ThreeSuppliers).GetAsync(Origin, limit: 1);

        Assert.Equal(Origin.Lat, result.Origin.Lat);
        Assert.Equal(Origin.Lng, result.Origin.Lng);
    }

    [Fact]
    public async Task GetAsync_CarriesTheDerivedCropListAndTheStringId()
    {
        var result = await Build(ThreeSuppliers).GetAsync(Origin, limit: 10);

        var nuevaEcija = result.Suppliers.Single(s => s.Name == "Nueva Ecija Grain Cooperative");

        Assert.Equal("2", nuevaEcija.Id);
        Assert.Equal(["rice", "corn"], nuevaEcija.Crops);
        Assert.True(nuevaEcija.Verified);
    }

    /// <summary>
    /// Bataan is roughly 50 km from Manila. Pinned loosely, to catch a latitude/longitude swap
    /// reaching the haversine through this service's argument order rather than through the
    /// formula's.
    /// </summary>
    [Fact]
    public async Task GetAsync_DistanceIsMeasuredFromTheOriginToTheSupplierNotTheOtherWayRound()
    {
        var result = await Build(ThreeSuppliers).GetAsync(Origin, limit: 1);

        Assert.InRange(result.Suppliers[0].DistanceKm, 40.0, 60.0);
    }
}
