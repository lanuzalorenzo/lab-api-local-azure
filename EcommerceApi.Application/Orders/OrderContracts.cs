using EcommerceApi.Domain.Orders;

namespace EcommerceApi.Application.Orders;

/// <summary>
/// Read model for orders. The query side never returns the aggregate: it returns a
/// flat projection that is safe to serialize and cheap to shape.
/// </summary>
public sealed record OrderSummary(int Id, DateTime CreatedAt, IReadOnlyList<int> ProductIds, decimal Total)
{
    public static OrderSummary From(Order order) =>
        new(order.Id, order.CreatedAt, order.ProductIds, order.Total.Amount);
}

/// <summary>
/// Options for the order list. <see cref="AfterId"/> is a keyset cursor: the page
/// starts after that identifier. <see cref="From"/> is inclusive and
/// <see cref="To"/> is exclusive, both expressed as instants.
/// </summary>
public sealed record OrderQuery(
    int Limit = 20,
    int? AfterId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null)
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;
}

/// <summary>
/// One page of orders. <see cref="NextCursor"/> is the identifier to pass as
/// <c>afterId</c> for the next page, or null when the page is the last one.
/// </summary>
public sealed record OrderPage(IReadOnlyList<OrderSummary> Items, int? NextCursor);

public interface ICreateOrderHandler
{
    Task<CreateOrderResult> HandleAsync(CreateOrderCommand command, CancellationToken cancellationToken = default);
}

public interface IGetOrdersHandler
{
    Task<OrderPage> HandleAsync(
        string userId,
        OrderQuery query,
        CancellationToken cancellationToken = default);
}

public interface IGetOrderByIdHandler
{
    Task<OrderSummary?> HandleAsync(int id, string userId, CancellationToken cancellationToken = default);
}
