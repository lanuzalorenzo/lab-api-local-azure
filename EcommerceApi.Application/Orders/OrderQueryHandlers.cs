using EcommerceApi.Application.Abstractions;

namespace EcommerceApi.Application.Orders;

public sealed class GetOrdersHandler : IGetOrdersHandler
{
    private readonly IOrderReadModel _orders;

    public GetOrdersHandler(IOrderReadModel orders) => _orders = orders;

    public Task<OrderPage> HandleAsync(
        string userId,
        OrderQuery query,
        CancellationToken cancellationToken = default) =>
        _orders.GetByUserAsync(userId, query, cancellationToken);
}

public sealed class GetOrderByIdHandler : IGetOrderByIdHandler
{
    private readonly IOrderReadModel _orders;

    public GetOrderByIdHandler(IOrderReadModel orders) => _orders = orders;

    public Task<OrderSummary?> HandleAsync(
        int id,
        string userId,
        CancellationToken cancellationToken = default) =>
        _orders.FindAsync(id, userId, cancellationToken);
}
