using AniKo_API.Models;
using AniKo_API.Repositories;
using AniKo_API.Services;

namespace AniKo_API.Tests.Services;

public class RecentOrdersServiceTests
{
    private static readonly DateTime Now = new(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc);

    private static (RecentOrdersService Service, FakeOrderRepository Repository) Build(
        params RecentOrderRow[] rows)
    {
        var repository = new FakeOrderRepository { RecentRows = rows };
        return (new RecentOrdersService(repository), repository);
    }

    /// <summary>
    /// "Recent" means newest by <c>CreatedAt</c> and that definition lives in the repository's
    /// ORDER BY. Re-sorting here would create a second, silently divergent definition of the word.
    /// </summary>
    [Fact]
    public async Task GetAsync_PreservesTheRepositoryOrdering()
    {
        var (service, _) = Build(
            Rows.Recent("AK-1003", Now.AddDays(-1)),
            Rows.Recent("AK-1002", Now.AddDays(-6)),
            Rows.Recent("AK-1001", Now.AddDays(-11)));

        var result = await service.GetAsync(limit: 10);

        Assert.Equal(["AK-1003", "AK-1002", "AK-1001"], [.. result.Orders.Select(o => o.Id)]);
    }

    [Fact]
    public async Task GetAsync_PassesTheLimitThroughToTheRepository()
    {
        var (service, repository) = Build(
            Rows.Recent("AK-1003", Now.AddDays(-1)),
            Rows.Recent("AK-1002", Now.AddDays(-6)),
            Rows.Recent("AK-1001", Now.AddDays(-11)));

        var result = await service.GetAsync(limit: 2);

        Assert.Equal(2, repository.LastRecentLimit);
        Assert.Equal(2, result.Orders.Count);
    }

    [Fact]
    public async Task GetAsync_NoOrders_YieldsAnEmptyListRatherThanNull()
    {
        var (service, _) = Build();

        var result = await service.GetAsync(limit: 10);

        Assert.NotNull(result.Orders);
        Assert.Empty(result.Orders);
    }

    /// <summary>
    /// The lowercase status, asserted for all four values. A badge keyed on "Confirmed" instead
    /// of "confirmed" loses its colour and its translation on a page that otherwise loads
    /// perfectly, with a 200 in the network tab and nothing in any log on either side.
    /// </summary>
    [Theory]
    [InlineData(OrderStatus.Confirmed, "confirmed")]
    [InlineData(OrderStatus.Processing, "processing")]
    [InlineData(OrderStatus.Shipped, "shipped")]
    [InlineData(OrderStatus.Delivered, "delivered")]
    public async Task GetAsync_EmitsTheLowercaseStatusKey(OrderStatus status, string expected)
    {
        var (service, _) = Build(Rows.Recent("AK-1001", Now.AddDays(-1), status));

        var order = Assert.Single((await service.GetAsync(limit: 10)).Orders);

        Assert.Equal(expected, order.Status);
    }

    /// <summary>
    /// The wire id is the human reference, not the surrogate key: the frontend renders it
    /// directly in the table's first column, and a buyer quoting "7" to support helps nobody.
    /// </summary>
    [Fact]
    public async Task GetAsync_MapsThroughDashboardMappers()
    {
        var createdAt = Now.AddDays(-1);
        var (service, _) = Build(Rows.Recent("AK-1007", createdAt));

        var order = Assert.Single((await service.GetAsync(limit: 10)).Orders);

        Assert.Equal("AK-1007", order.Id);
        Assert.Equal("Lot for AK-1007", order.Product);
        Assert.Equal("Bataan Rice Growers", order.Supplier);
        Assert.Equal(1_500, order.QuantityKg);
        Assert.Equal(
            DateOnly.FromDateTime(createdAt.AddDays(14)).ToString("yyyy-MM-dd"),
            order.EstimatedDelivery);
    }
}
