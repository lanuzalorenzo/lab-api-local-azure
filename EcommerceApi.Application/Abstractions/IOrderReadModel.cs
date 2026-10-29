using EcommerceApi.Application.Orders;

namespace EcommerceApi.Application.Abstractions;

/// <summary>
/// Read-side port for orders. Every method is scoped to the owner so that object
/// level authorization cannot be bypassed by an application service.
/// </summary>
public interface IOrderReadModel
{
    Task<OrderPage> GetByUserAsync(
        string userId,
        OrderQuery query,
        CancellationToken cancellationToken = default);

    Task<OrderSummary?> FindAsync(
        int id,
        string userId,
        CancellationToken cancellationToken = default);
}
