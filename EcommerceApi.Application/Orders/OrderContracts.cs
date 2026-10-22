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

public interface ICreateOrderHandler
{
    Task<CreateOrderResult> HandleAsync(CreateOrderCommand command, CancellationToken cancellationToken = default);
}

public interface IGetOrdersHandler
{
    IReadOnlyList<OrderSummary> Handle(string userId);
}

public interface IGetOrderByIdHandler
{
    OrderSummary? Handle(int id, string userId);
}
