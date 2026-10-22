using EcommerceApi.Application.Orders;
using EcommerceApi.Domain.Common;
using EcommerceApi.Domain.Orders;
using EcommerceApi.UnitTests.Fakes;
using Xunit;

namespace EcommerceApi.UnitTests.Application;

public sealed class OrderQueryHandlerTests
{
    private static readonly DateTime CreatedAt = new(2026, 10, 22, 10, 0, 0, DateTimeKind.Utc);

    private static Order Line(string owner, int productId) =>
        Order.Create(owner, [new OrderLine(productId, Money.From(10m), 1)], CreatedAt);

    [Fact]
    public void GetOrders_ReturnsOnlyTheCallersOrders()
    {
        var repository = new FakeOrderRepository();
        var first = repository.Add(Line("owner", 1));
        var second = repository.Add(Line("owner", 2));
        repository.Add(Line("other", 3));
        var handler = new GetOrdersHandler(repository);

        var result = handler.Handle("owner");

        Assert.Equal([first.Id, second.Id], result.Select(summary => summary.Id));
    }

    [Fact]
    public void GetOrderById_ForAnotherUser_ReturnsNull()
    {
        var repository = new FakeOrderRepository();
        var order = repository.Add(Line("owner", 1));
        var handler = new GetOrderByIdHandler(repository);

        var result = handler.Handle(order.Id, "other");

        Assert.Null(result);
    }

    [Fact]
    public void GetOrderById_ForOwner_ReturnsProjection()
    {
        var repository = new FakeOrderRepository();
        var order = repository.Add(Line("owner", 7));
        var handler = new GetOrderByIdHandler(repository);

        var result = handler.Handle(order.Id, "owner");

        Assert.NotNull(result);
        Assert.Equal([7], result.ProductIds);
        Assert.Equal(10m, result.Total);
    }
}
