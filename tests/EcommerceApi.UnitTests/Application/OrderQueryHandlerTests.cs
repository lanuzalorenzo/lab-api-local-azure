using EcommerceApi.Application.Orders;
using EcommerceApi.UnitTests.Fakes;
using Xunit;

namespace EcommerceApi.UnitTests.Application;

public sealed class OrderQueryHandlerTests
{
    private static readonly DateTime CreatedAt = new(2026, 10, 22, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetOrders_ReturnsOnlyTheCallersOrders()
    {
        var readModel = new FakeOrderReadModel();
        readModel.Add("owner", new OrderSummary(1, CreatedAt, [1], 10m));
        readModel.Add("owner", new OrderSummary(2, CreatedAt, [2], 20m));
        readModel.Add("other", new OrderSummary(3, CreatedAt, [3], 30m));
        var handler = new GetOrdersHandler(readModel);

        var result = await handler.HandleAsync("owner");

        Assert.Equal([1, 2], result.Select(summary => summary.Id));
    }

    [Fact]
    public async Task GetOrderById_ForAnotherUser_ReturnsNull()
    {
        var readModel = new FakeOrderReadModel();
        readModel.Add("owner", new OrderSummary(1, CreatedAt, [1], 10m));
        var handler = new GetOrderByIdHandler(readModel);

        var result = await handler.HandleAsync(1, "other");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetOrderById_ForOwner_ReturnsProjection()
    {
        var readModel = new FakeOrderReadModel();
        readModel.Add("owner", new OrderSummary(7, CreatedAt, [7], 10m));
        var handler = new GetOrderByIdHandler(readModel);

        var result = await handler.HandleAsync(7, "owner");

        Assert.NotNull(result);
        Assert.Equal([7], result.ProductIds);
        Assert.Equal(10m, result.Total);
    }
}
