using AniKo_API.Dtos;
using AniKo_API.Services;
using AniKo_API.Validation;

namespace AniKo_API.Endpoints;

/// <summary>
/// The five read-only endpoints behind the buyer dashboard.
/// </summary>
/// <remarks>
/// <para>
/// Every handler here is three lines and does nothing but bind, delegate, and wrap in
/// <c>Ok</c>. That is deliberate: the moment a handler starts shaping data, that shaping becomes
/// untestable without a host, and the service layer stops being the place the behaviour lives.
/// If a handler below ever grows a conditional, it belongs in the service.
/// </para>
/// <para>
/// <b>Routes are versioned from the first commit.</b> <c>/api/v1</c> costs nothing now; adding it
/// later means either breaking a deployed frontend or serving both forever.
/// </para>
/// </remarks>
public static class DashboardEndpoints
{
    /// <summary>The shared prefix. One constant so the tests assert against the same string the
    /// routes are built from, rather than a copy that can drift.</summary>
    public const string BasePath = "/api/v1";

    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder routes)
    {
        // Tagged once on the group rather than per endpoint — all five belong to one panel set,
        // and repeating the tag five times invites the sixth to be forgotten.
        //
        // Deliberately no .WithOpenApi(): it is obsolete as of .NET 10 (ASPDEPR002). The document
        // generator now reads WithSummary/WithDescription/WithTags metadata directly, so the call
        // was not adding anything — it was a no-op that would have become a build error later.
        var api = routes.MapGroup(BasePath).WithTags("Dashboard");

        // --- Overview stats ---------------------------------------------------------------
        // No request record and so no validation filter: this endpoint takes no parameters at
        // all. Windows are computed from the injected TimeProvider, not from the caller.
        api.MapGet("/buyer/overview/stats",
            async (IOverviewStatsService service, CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.GetAsync(cancellationToken)))
            .WithName("GetOverviewStats")
            .WithSummary("The four buyer stat tiles with period-on-period deltas.")
            .WithDescription(
                "Active orders, spend, distinct suppliers and average market price, each with " +
                "its change against the preceding 30-day window. Always returns all four keys " +
                "in a stable order; a tile with no data reports zero rather than being omitted, " +
                "because the frontend renders the grid from the response.");

        // --- Price trends -----------------------------------------------------------------
        api.MapGet("/pricing/trends",
            async (
                [AsParameters] PriceTrendsRequest request,
                IPriceTrendsService service,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.GetAsync(request.Months, cancellationToken)))
            .WithValidation<PriceTrendsRequest>()
            .WithName("GetPriceTrends")
            .WithSummary("Market price history, pivoted one row per month.")
            .WithDescription(
                "Each point carries a date and a price for every crop, which is the row shape " +
                "the chart consumes directly. `months` must be in [1, 24]; out of range is a " +
                "400, never a silent clamp. Omitting it means 6.");

        // --- Nearby suppliers -------------------------------------------------------------
        api.MapGet("/suppliers/nearby",
            async (
                [AsParameters] NearbySuppliersRequest request,
                INearbySupplierService service,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.GetAsync(
                    new LatLngDto(request.Lat, request.Lng), request.Limit, cancellationToken)))
            .WithValidation<NearbySuppliersRequest>()
            .WithName("GetNearbySuppliers")
            .WithSummary("Verified suppliers ranked by distance from a buyer position.")
            .WithDescription(
                "`lat` and `lng` are required and have no default: (0, 0) is a real coordinate, " +
                "so defaulting them would rank Philippine suppliers by their distance from the " +
                "Gulf of Guinea and return a plausible list in the wrong order. The origin is " +
                "echoed back so the list and the map cannot disagree.");

        // --- Featured lots ----------------------------------------------------------------
        api.MapGet("/listings/featured",
            async (
                [AsParameters] FeaturedLotsRequest request,
                IFeaturedLotsService service,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.GetAsync(request.Limit, cancellationToken)))
            .WithValidation<FeaturedLotsRequest>()
            .WithName("GetFeaturedLots")
            .WithSummary("Featured wholesale lots with supplier and crop resolved.")
            .WithDescription("`limit` must be in [1, 50]. Omitting it means 10.");

        // --- Recent orders ----------------------------------------------------------------
        api.MapGet("/orders/recent",
            async (
                [AsParameters] RecentOrdersRequest request,
                IRecentOrdersService service,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.GetAsync(request.Limit, cancellationToken)))
            .WithValidation<RecentOrdersRequest>()
            .WithName("GetRecentOrders")
            .WithSummary("The most recently placed orders, newest first.")
            .WithDescription(
                "`id` is the human reference (\"AK-1003\"), not the surrogate key, and `status` " +
                "is lowercase to match the frontend's badge and translation lookups. " +
                "`limit` must be in [1, 50]. Omitting it means 10.");

        return routes;
    }
}
