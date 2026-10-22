using System.Collections.Concurrent;
using EcommerceApi.Application.Abstractions;
using EcommerceApi.Domain.Orders;

namespace EcommerceApi.Infrastructure.Persistence;

/// <summary>
/// Ephemeral, thread-safe implementation of the order port. Ordering and ownership
/// are enforced here, so every adapter must keep the same contract.
/// </summary>
public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly ConcurrentDictionary<int, Order> _orders = new();
    private int _nextId;

    public IReadOnlyList<Order> GetByUser(string userId) =>
        _orders.Values
            .Where(order => string.Equals(order.UserId, userId, StringComparison.Ordinal))
            .OrderBy(order => order.Id)
            .ToList();

    public Order? Find(int id, string userId) =>
        _orders.TryGetValue(id, out var order) &&
        string.Equals(order.UserId, userId, StringComparison.Ordinal)
            ? order
            : null;

    public Order Add(Order order)
    {
        order.AssignIdentity(Interlocked.Increment(ref _nextId));
        _orders[order.Id] = order;
        return order;
    }
}
