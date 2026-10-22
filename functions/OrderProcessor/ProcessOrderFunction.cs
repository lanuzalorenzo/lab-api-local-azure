using Azure.Messaging.ServiceBus;
using EcommerceApi.Application.OrderProcessing;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace OrderProcessor;

/// <summary>
/// Service Bus triggered consumer. It only adapts transport to the use case:
/// deserialize the broker message, delegate to <see cref="IProcessOrderHandler"/>
/// and let the trigger retry (and eventually dead-letter) on rejection.
/// </summary>
public sealed class ProcessOrderFunction
{
    private readonly IProcessOrderHandler _handler;
    private readonly ILogger<ProcessOrderFunction> _logger;

    public ProcessOrderFunction(IProcessOrderHandler handler, ILogger<ProcessOrderFunction> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    [Function("ProcessOrder")]
    public async Task Run(
        [ServiceBusTrigger("%ServiceBus:QueueName%", Connection = "ServiceBus")]
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        var result = await _handler.HandleAsync(
            message.MessageId,
            message.Body.ToString(),
            cancellationToken);

        if (result.Duplicate)
        {
            _logger.LogInformation(
                "Message {MessageId} was already processed; delivery is acknowledged without repeating effects.",
                message.MessageId);
            return;
        }

        if (!result.Succeeded)
        {
            // Throwing makes Service Bus retry and, after maxDeliveryCount, move the
            // message to the dead-letter queue instead of losing it.
            throw new InvalidOperationException(
                $"Rejected order message {message.MessageId}: {string.Join(" ", result.Errors)}");
        }

        _logger.LogInformation(
            "Order message {MessageId} processed successfully (delivery count {DeliveryCount}).",
            message.MessageId,
            message.DeliveryCount);
    }
}
