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

        var result = await handler.HandleAsync("owner", new OrderQuery());

        Assert.Equal([1, 2], result.Items.Select(summary => summary.Id));
    }

    [Fact]
    public async Task GetOrders_WithLimitAndCursor_ReturnsTheNextPage()
    {
        var readModel = new FakeOrderReadModel();
        readModel.Add("owner", new OrderSummary(1, CreatedAt, [1], 10m));
        readModel.Add("owner", new OrderSummary(2, CreatedAt, [2], 20m));
        readModel.Add("owner", new OrderSummary(3, CreatedAt, [3], 30m));
        var handler = new GetOrdersHandler(readModel);

        var first = await handler.HandleAsync("owner", new OrderQuery(Limit: 2));
        var second = await handler.HandleAsync(
            "owner",
            new OrderQuery(Limit: 2, AfterId: first.NextCursor));

        Assert.Equal([1, 2], first.Items.Select(summary => summary.Id));
        Assert.Equal(2, first.NextCursor);
        Assert.Equal([3], second.Items.Select(summary => summary.Id));
        Assert.Null(second.NextCursor);
    }

    [Fact]
    public async Task GetOrders_ForwardsTheQueryOptions()
    {
        var readModel = new FakeOrderReadModel();
        var handler = new GetOrdersHandler(readModel);
        var from = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

        await handler.HandleAsync("owner", new OrderQuery(10, 5, from, to));

        Assert.NotNull(readModel.LastQuery);
        Assert.Equal(10, readModel.LastQuery!.Limit);
        Assert.Equal(5, readModel.LastQuery.AfterId);
        Assert.Equal(from, readModel.LastQuery.From);
        Assert.Equal(to, readModel.LastQuery.To);
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
