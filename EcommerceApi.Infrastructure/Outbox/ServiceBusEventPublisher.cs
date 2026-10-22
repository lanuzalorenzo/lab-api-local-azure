using Azure.Messaging.ServiceBus;
using EcommerceApi.Application.Abstractions;
using EcommerceApi.Application.Outbox;

namespace EcommerceApi.Infrastructure.Outbox;

public sealed class ServiceBusOptions
{
    public const string SectionName = "ServiceBus";

    public string ConnectionString { get; set; } = string.Empty;

    public string QueueName { get; set; } = "orders";
}

/// <summary>
/// Azure Service Bus adapter. It is selected only when <c>Publisher:Mode</c> is
/// <c>ServiceBus</c>. Without a real namespace and queue it is not exercised by the
/// test suite; see ADR-0004 for the declared state.
/// </summary>
public sealed class ServiceBusEventPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _sender;

    public ServiceBusEventPublisher(ServiceBusOptions options)
    {
        _client = new ServiceBusClient(options.ConnectionString);
        _sender = _client.CreateSender(options.QueueName);
    }

    public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        var busMessage = new ServiceBusMessage(message.Payload)
        {
            MessageId = message.MessageId,
            Subject = message.Type,
            ContentType = "application/json"
        };

        await _sender.SendMessageAsync(busMessage, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
        await _client.DisposeAsync();
    }
}
