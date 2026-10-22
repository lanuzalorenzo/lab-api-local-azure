using System.Text.Json;
using EcommerceApi.Application.Abstractions;
using EcommerceApi.Application.Outbox;
using EcommerceApi.Contracts;
using EcommerceApi.Domain.Orders;

namespace EcommerceApi.Application.Orders;

public sealed class CreateOrderHandler : ICreateOrderHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IProductRepository _products;
    private readonly IOrderRepository _orders;
    private readonly IOutboxWriter _outbox;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public CreateOrderHandler(
        IProductRepository products,
        IOrderRepository orders,
        IOutboxWriter outbox,
        IUnitOfWork unitOfWork,
        TimeProvider clock)
    {
        _products = products;
        _orders = orders;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<CreateOrderResult> HandleAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        var productIds = command.ProductIds.Distinct().ToList();

        if (productIds.Count == 0)
        {
            return CreateOrderResult.Invalid(["An order must contain at least one product."]);
        }

        if (productIds.Count > Order.MaxLines)
        {
            return CreateOrderResult.Invalid(
                [$"An order cannot contain more than {Order.MaxLines} products."]);
        }

        var missingProductIds = new List<int>();
        var lines = new List<OrderLine>(productIds.Count);

        foreach (var productId in productIds)
        {
            var product = await _products.FindAsync(productId, cancellationToken);
            if (product is null)
            {
                missingProductIds.Add(productId);
                continue;
            }

            lines.Add(new OrderLine(product.Id, product.Price, quantity: 1));
        }

        if (missingProductIds.Count > 0)
        {
            return CreateOrderResult.MissingProducts(missingProductIds);
        }

        var occurredAt = _clock.GetUtcNow().UtcDateTime;

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var order = Order.Create(command.UserId, lines, occurredAt);
        await _orders.AddAsync(order, cancellationToken);

        var domainEvent = order.DomainEvents.OfType<OrderPlacedDomainEvent>().Single();
        var integrationEvent = domainEvent.ToIntegrationEvent();
        var payload = JsonSerializer.Serialize(integrationEvent, SerializerOptions);

        await _outbox.AddAsync(
            new OutboxMessage(integrationEvent.MessageId, OrderCreatedV1.Type, payload, occurredAt),
            cancellationToken);

        order.ClearDomainEvents();

        await transaction.CommitAsync(cancellationToken);

        return CreateOrderResult.Success(order);
    }
}
