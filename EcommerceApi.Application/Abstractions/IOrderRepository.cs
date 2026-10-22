using EcommerceApi.Domain.Orders;

namespace EcommerceApi.Application.Abstractions;

/// <summary>
/// Outbound port for orders. Reads are always scoped to the owner so that object
/// level authorization cannot be bypassed by an application service.
/// </summary>
public interface IOrderRepository
{
    IReadOnlyList<Order> GetByUser(string userId);

    Order? Find(int id, string userId);

    Order Add(Order order);
}
