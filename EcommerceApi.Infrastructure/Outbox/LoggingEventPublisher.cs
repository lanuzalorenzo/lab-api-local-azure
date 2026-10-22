using EcommerceApi.Application.Abstractions;
using EcommerceApi.Application.Outbox;
using Microsoft.Extensions.Logging;

namespace EcommerceApi.Infrastructure.Outbox;

/// <summary>
/// Local publisher used by default. It proves the outbox pipeline end to end without
/// pretending that a broker exists: the message is logged, not delivered anywhere.
/// </summary>
public sealed class LoggingEventPublisher : IEventPublisher
{
    private readonly ILogger<LoggingEventPublisher> _logger;

    public LoggingEventPublisher(ILogger<LoggingEventPublisher> logger) => _logger = logger;

    public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Outbox message {MessageId} ({Type}) published to the local sink at {OccurredAt}.",
            message.MessageId,
            message.Type,
            message.OccurredAt);

        return Task.CompletedTask;
    }
}
