using EcommerceApi.Application.Abstractions;

namespace EcommerceApi.Application.Orders;

public sealed class GetOrdersHandler : IGetOrdersHandler
{
    private readonly IOrderRepository _orders;

    public GetOrdersHandler(IOrderRepository orders) => _orders = orders;

    public IReadOnlyList<OrderSummary> Handle(string userId) =>
        _orders.GetByUser(userId).Select(OrderSummary.From).ToList();
}

public sealed class GetOrderByIdHandler : IGetOrderByIdHandler
{
    private readonly IOrderRepository _orders;

    public GetOrderByIdHandler(IOrderRepository orders) => _orders = orders;

    public OrderSummary? Handle(int id, string userId)
    {
        var order = _orders.Find(id, userId);

        return order is null ? null : OrderSummary.From(order);
    }
}
