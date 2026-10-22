using EcommerceApi.Domain.Common;

namespace EcommerceApi.Domain.Orders;

/// <summary>
/// Fact raised when an order is created. It carries the aggregate so that the
/// application layer reads the identity after persistence; the integration message
/// (<c>OrderCreatedV1</c>) is built from it, not from the domain event directly.
/// </summary>
public sealed record OrderPlacedDomainEvent(Order Order, DateTime OccurredAt) : IDomainEvent
{
    public int OrderId => Order.Id;

    public string UserId => Order.UserId;

    public decimal Total => Order.Total.Amount;
}
