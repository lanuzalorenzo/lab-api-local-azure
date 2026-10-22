using EcommerceApi.Application.Abstractions;
using EcommerceApi.Domain.Orders;

namespace EcommerceApi.Application.Orders;

public sealed class CreateOrderHandler : ICreateOrderHandler
{
    private readonly IProductRepository _products;
    private readonly IOrderRepository _orders;
    private readonly TimeProvider _clock;

    public CreateOrderHandler(
        IProductRepository products,
        IOrderRepository orders,
        TimeProvider clock)
    {
        _products = products;
        _orders = orders;
        _clock = clock;
    }

    public Task<CreateOrderResult> HandleAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        var productIds = command.ProductIds.Distinct().ToList();

        if (productIds.Count == 0)
        {
            return Task.FromResult(CreateOrderResult.Invalid(["An order must contain at least one product."]));
        }

        if (productIds.Count > Order.MaxLines)
        {
            return Task.FromResult(CreateOrderResult.Invalid(
                [$"An order cannot contain more than {Order.MaxLines} products."]));
        }

        var missingProductIds = productIds
            .Where(productId => _products.Find(productId) is null)
            .ToList();

        if (missingProductIds.Count > 0)
        {
            return Task.FromResult(CreateOrderResult.MissingProducts(missingProductIds));
        }

        var lines = productIds
            .Select(productId => new OrderLine(productId, _products.Find(productId)!.Price, quantity: 1))
            .ToList();

        var order = Order.Create(command.UserId, lines, _clock.GetUtcNow().UtcDateTime);
        _orders.Add(order);

        return Task.FromResult(CreateOrderResult.Success(order));
    }
}
