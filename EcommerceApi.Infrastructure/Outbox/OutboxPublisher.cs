using EcommerceApi.Application.Abstractions;
using EcommerceApi.Application.Outbox;
using Microsoft.Extensions.Logging;

namespace EcommerceApi.Infrastructure.Outbox;

/// <summary>
/// Publishes pending outbox rows and records the delivery outcome. It is separated
/// from the hosted service so the retry and dead-letter behaviour can be tested
/// without a timer.
/// </summary>
public sealed class OutboxPublisher
{
    public const int MaxAttempts = 5;
    public const int BatchSize = 20;

    private readonly IOutboxStore _store;
    private readonly IEventPublisher _publisher;
    private readonly TimeProvider _clock;
    private readonly ILogger<OutboxPublisher> _logger;

    public OutboxPublisher(
        IOutboxStore store,
        IEventPublisher publisher,
        TimeProvider clock,
        ILogger<OutboxPublisher> logger)
    {
        _store = store;
        _publisher = publisher;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> PublishPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _store.GetPendingAsync(BatchSize, cancellationToken);
        var published = 0;

        foreach (var message in pending)
        {
            try
            {
                await _publisher.PublishAsync(
                    new OutboxMessage(
                        message.MessageId,
                        message.Type,
                        message.Payload,
                        message.OccurredAt),
                    cancellationToken);

                await _store.MarkProcessedAsync(
                    message.Id,
                    _clock.GetUtcNow().UtcDateTime,
                    cancellationToken);

                published++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var attempts = message.Attempts + 1;
                var deadLetter = attempts >= MaxAttempts;

                _logger.LogError(
                    exception,
                    "Outbox message {MessageId} failed on attempt {Attempts}. Dead-lettered: {DeadLettered}.",
                    message.MessageId,
                    attempts,
                    deadLetter);

                await _store.MarkFailedAsync(
                    message.Id,
                    attempts,
                    exception.Message,
                    deadLetter,
                    _clock.GetUtcNow().UtcDateTime,
                    cancellationToken);
            }
        }

        return published;
    }
}
