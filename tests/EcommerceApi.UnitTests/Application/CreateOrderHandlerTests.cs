using System.Text.Json;
using EcommerceApi.Application.Orders;
using EcommerceApi.Contracts;
using EcommerceApi.Domain.Products;
using EcommerceApi.UnitTests.Fakes;
using Xunit;

namespace EcommerceApi.UnitTests.Application;

public sealed class CreateOrderHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);

    private static (
        CreateOrderHandler Handler,
        FakeOrderRepository Orders,
        FakeOutboxWriter Outbox,
        FakeUnitOfWork UnitOfWork) CreateSut(params Product[] products)
    {
        var productRepository = new FakeProductRepository(products);
        var orderRepository = new FakeOrderRepository();
        var outbox = new FakeOutboxWriter();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateOrderHandler(
            productRepository,
            orderRepository,
            outbox,
            unitOfWork,
            new FixedTimeProvider(Now));

        return (handler, orderRepository, outbox, unitOfWork);
    }

    [Fact]
    public async Task HandleAsync_WithUnknownProduct_ReturnsMissingProducts()
    {
        var (handler, _, _, _) = CreateSut(Product.Create("Laptop", 100m, 1));

        var result = await handler.HandleAsync(new CreateOrderCommand("user-1", [1, 999]));

        Assert.False(result.Succeeded);
        Assert.Equal([999], result.MissingProductIds);
    }

    [Fact]
    public async Task HandleAsync_UsesCataloguePrice()
    {
        var (handler, _, _, _) = CreateSut(
            Product.Create("Laptop", 100m, 1),
            Product.Create("Mouse", 10m, 1));

        var result = await handler.HandleAsync(new CreateOrderCommand("user-1", [1, 2]));

        Assert.True(result.Succeeded);
        Assert.Equal(110m, result.Order!.Total.Amount);
    }

    [Fact]
    public async Task HandleAsync_DeduplicatesProductIds()
    {
        var (handler, _, _, _) = CreateSut(Product.Create("Laptop", 100m, 1));

        var result = await handler.HandleAsync(new CreateOrderCommand("user-1", [1, 1]));

        Assert.True(result.Succeeded);
        Assert.Single(result.Order!.Lines);
        Assert.Equal(100m, result.Order.Total.Amount);
    }

    [Fact]
    public async Task HandleAsync_AssignsOwnerAndClock()
    {
        var (handler, _, _, _) = CreateSut(Product.Create("Laptop", 100m, 1));

        var result = await handler.HandleAsync(new CreateOrderCommand("user-42", [1]));

        Assert.Equal("user-42", result.Order!.UserId);
        Assert.Equal(Now.UtcDateTime, result.Order.CreatedAt);
    }

    [Fact]
    public async Task HandleAsync_PersistsTheOrderWithIdentity()
    {
        var (handler, orders, _, _) = CreateSut(Product.Create("Laptop", 100m, 1));

        var result = await handler.HandleAsync(new CreateOrderCommand("user-1", [1]));

        Assert.NotNull(result.Order);
        Assert.True(result.Order.Id > 0);
        Assert.Contains(orders.Items, order => order.Id == result.Order.Id);
    }

    [Fact]
    public async Task HandleAsync_CommitsTheTransaction()
    {
        var (handler, _, _, unitOfWork) = CreateSut(Product.Create("Laptop", 100m, 1));

        await handler.HandleAsync(new CreateOrderCommand("user-1", [1]));

        Assert.True(unitOfWork.Committed);
    }

    [Fact]
    public async Task HandleAsync_WritesTheOutboxMessageInTheSameTransaction()
    {
        var (handler, _, outbox, _) = CreateSut(Product.Create("Laptop", 100m, 1));

        var result = await handler.HandleAsync(new CreateOrderCommand("user-1", [1]));

        var message = Assert.Single(outbox.Messages);
        Assert.Equal(OrderCreatedV1.Type, message.Type);
        Assert.Equal($"{result.Order!.Id}:v1", message.MessageId);

        var integrationEvent = JsonSerializer.Deserialize<OrderCreatedV1>(
            message.Payload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(integrationEvent);
        Assert.Equal(result.Order.Id, integrationEvent.OrderId);
        Assert.Equal("user-1", integrationEvent.UserId);
        Assert.Equal(100m, integrationEvent.Total);
        Assert.Single(integrationEvent.Items);
    }

    [Fact]
    public async Task HandleAsync_WithNoProducts_ReturnsInvalidWithoutCommitting()
    {
        var (handler, _, outbox, unitOfWork) = CreateSut();

        var result = await handler.HandleAsync(new CreateOrderCommand("user-1", []));

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Errors);
        Assert.False(unitOfWork.Committed);
        Assert.Empty(outbox.Messages);
    }
}
