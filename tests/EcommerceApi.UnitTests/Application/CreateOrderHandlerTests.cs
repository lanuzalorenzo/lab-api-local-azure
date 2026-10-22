using EcommerceApi.Application.Orders;
using EcommerceApi.Domain.Products;
using EcommerceApi.UnitTests.Fakes;
using Xunit;

namespace EcommerceApi.UnitTests.Application;

public sealed class CreateOrderHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);

    private static (CreateOrderHandler Handler, FakeProductRepository Products, FakeOrderRepository Orders)
        CreateSut(params Product[] products)
    {
        var productRepository = new FakeProductRepository(products);
        var orderRepository = new FakeOrderRepository();
        var handler = new CreateOrderHandler(productRepository, orderRepository, new FixedTimeProvider(Now));

        return (handler, productRepository, orderRepository);
    }

    [Fact]
    public async Task HandleAsync_WithUnknownProduct_ReturnsMissingProducts()
    {
        var (handler, _, _) = CreateSut(Product.Create("Laptop", 100m, 1));

        var result = await handler.HandleAsync(new CreateOrderCommand("user-1", [1, 999]));

        Assert.False(result.Succeeded);
        Assert.Equal([999], result.MissingProductIds);
    }

    [Fact]
    public async Task HandleAsync_UsesCataloguePrice()
    {
        var (handler, _, _) = CreateSut(
            Product.Create("Laptop", 100m, 1),
            Product.Create("Mouse", 10m, 1));

        var result = await handler.HandleAsync(new CreateOrderCommand("user-1", [1, 2]));

        Assert.True(result.Succeeded);
        Assert.Equal(110m, result.Order!.Total.Amount);
    }

    [Fact]
    public async Task HandleAsync_DeduplicatesProductIds()
    {
        var (handler, _, _) = CreateSut(Product.Create("Laptop", 100m, 1));

        var result = await handler.HandleAsync(new CreateOrderCommand("user-1", [1, 1]));

        Assert.True(result.Succeeded);
        Assert.Single(result.Order!.Lines);
        Assert.Equal(100m, result.Order.Total.Amount);
    }

    [Fact]
    public async Task HandleAsync_AssignsOwnerAndClock()
    {
        var (handler, _, _) = CreateSut(Product.Create("Laptop", 100m, 1));

        var result = await handler.HandleAsync(new CreateOrderCommand("user-42", [1]));

        Assert.Equal("user-42", result.Order!.UserId);
        Assert.Equal(Now.UtcDateTime, result.Order.CreatedAt);
    }

    [Fact]
    public async Task HandleAsync_PersistsTheOrderWithIdentity()
    {
        var (handler, _, orders) = CreateSut(Product.Create("Laptop", 100m, 1));

        var result = await handler.HandleAsync(new CreateOrderCommand("user-1", [1]));

        Assert.NotNull(result.Order);
        Assert.True(result.Order.Id > 0);
        Assert.NotNull(orders.Find(result.Order.Id, "user-1"));
    }

    [Fact]
    public async Task HandleAsync_WithNoProducts_ReturnsInvalid()
    {
        var (handler, _, _) = CreateSut();

        var result = await handler.HandleAsync(new CreateOrderCommand("user-1", []));

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Errors);
    }
}
