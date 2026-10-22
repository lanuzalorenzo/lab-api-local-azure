using EcommerceApi.Application.Outbox;

namespace EcommerceApi.Application.Abstractions;

/// <summary>
/// Outbound port used by the outbox dispatcher. The local adapter logs the message;
/// the Azure adapter publishes it to Service Bus.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}
