using EcommerceApi.Application.Outbox;

namespace EcommerceApi.Application.Abstractions;

/// <summary>
/// Outbound port for the transactional outbox. Implementations must enlist in the
/// current transaction; they must not open a separate one.
/// </summary>
public interface IOutboxWriter
{
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}
