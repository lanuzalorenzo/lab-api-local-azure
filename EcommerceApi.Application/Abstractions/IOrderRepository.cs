using EcommerceApi.Domain.Orders;

namespace EcommerceApi.Application.Abstractions;

/// <summary>
/// Write-side port for orders. Reads live in <see cref="IOrderReadModel"/>: the
/// aggregate is loaded only when it must be mutated.
/// </summary>
public interface IOrderRepository
{
    Task AddAsync(Order order, CancellationToken cancellationToken = default);
}
