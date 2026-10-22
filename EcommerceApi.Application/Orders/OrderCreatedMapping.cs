using EcommerceApi.Contracts;
using EcommerceApi.Domain.Orders;

namespace EcommerceApi.Application.Orders;

/// <summary>
/// Translates the domain event into the versioned integration contract. The
/// translation lives in the application layer: the domain must not know that a
/// broker, a schema or a message id exist.
/// </summary>
internal static class OrderCreatedMapping
{
    public static OrderCreatedV1 ToIntegrationEvent(this OrderPlacedDomainEvent domainEvent)
    {
        var order = domainEvent.Order;

        return new OrderCreatedV1(
            OrderCreatedV1.BuildMessageId(order.Id),
            order.Id,
            order.UserId,
            order.CreatedAt,
            order.Lines
                .Select(line => new OrderCreatedLineV1(line.ProductId, line.UnitPrice.Amount, line.Quantity))
                .ToList(),
            order.Total.Amount);
    }
}
