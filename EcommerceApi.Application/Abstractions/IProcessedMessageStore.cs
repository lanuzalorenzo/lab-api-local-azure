namespace EcommerceApi.Application.Abstractions;

/// <summary>
/// Idempotency store for the consumer. Service Bus guarantees at-least-once
/// delivery, so processing the same message twice must not repeat its effects.
/// </summary>
public interface IProcessedMessageStore
{
    Task<bool> ExistsAsync(string messageId, CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(string messageId, DateTime processedAt, CancellationToken cancellationToken = default);
}
