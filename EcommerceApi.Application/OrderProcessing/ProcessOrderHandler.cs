using System.Text.Json;
using EcommerceApi.Application.Abstractions;
using EcommerceApi.Contracts;

namespace EcommerceApi.Application.OrderProcessing;

public sealed record ProcessOrderResult(bool Processed, bool Duplicate, IReadOnlyList<string> Errors)
{
    public bool Succeeded => Processed || Duplicate;

    public static ProcessOrderResult Accepted() => new(true, false, []);

    public static ProcessOrderResult AlreadyProcessed() => new(false, true, []);

    public static ProcessOrderResult Rejected(params string[] errors) => new(false, false, errors);
}

public interface IProcessOrderHandler
{
    Task<ProcessOrderResult> HandleAsync(
        string messageId,
        string payload,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Consumer-side use case for <c>OrderCreatedV1</c>. It is transport agnostic: the
/// Azure Function only deserializes the Service Bus message and delegates here.
/// Idempotency is keyed on the broker message id, because Service Bus guarantees
/// at-least-once delivery.
/// </summary>
public sealed class ProcessOrderHandler : IProcessOrderHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IProcessedMessageStore _processedMessages;
    private readonly TimeProvider _clock;

    public ProcessOrderHandler(IProcessedMessageStore processedMessages, TimeProvider clock)
    {
        _processedMessages = processedMessages;
        _clock = clock;
    }

    public async Task<ProcessOrderResult> HandleAsync(
        string messageId,
        string payload,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return ProcessOrderResult.Rejected("The broker message id is required.");
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            return ProcessOrderResult.Rejected("The message body is required.");
        }

        OrderCreatedV1? message;

        try
        {
            message = JsonSerializer.Deserialize<OrderCreatedV1>(payload, SerializerOptions);
        }
        catch (JsonException)
        {
            return ProcessOrderResult.Rejected("The message body is not valid JSON for order.created.v1.");
        }

        if (message is null)
        {
            return ProcessOrderResult.Rejected("The message body is empty.");
        }

        var validationErrors = Validate(message, messageId);
        if (validationErrors.Count > 0)
        {
            return ProcessOrderResult.Rejected([.. validationErrors]);
        }

        if (await _processedMessages.ExistsAsync(messageId, cancellationToken))
        {
            return ProcessOrderResult.AlreadyProcessed();
        }

        await _processedMessages.MarkProcessedAsync(
            messageId,
            _clock.GetUtcNow().UtcDateTime,
            cancellationToken);

        return ProcessOrderResult.Accepted();
    }

    private static List<string> Validate(OrderCreatedV1 message, string brokerMessageId)
    {
        var errors = new List<string>();

        if (!string.Equals(message.MessageId, brokerMessageId, StringComparison.Ordinal))
        {
            errors.Add("The payload message id does not match the broker message id.");
        }

        if (message.OrderId <= 0)
        {
            errors.Add("The order id must be positive.");
        }

        if (string.IsNullOrWhiteSpace(message.UserId))
        {
            errors.Add("The order must belong to a user.");
        }

        if (message.CreatedAt == default)
        {
            errors.Add("The creation timestamp is required.");
        }

        if (message.Items.Count == 0)
        {
            errors.Add("The order must contain at least one item.");
        }

        foreach (var item in message.Items)
        {
            if (item.ProductId <= 0)
            {
                errors.Add("Every item must reference a positive product id.");
            }

            if (item.Quantity <= 0)
            {
                errors.Add("Every item quantity must be greater than zero.");
            }

            if (item.UnitPrice < 0m)
            {
                errors.Add("Every item price must be non-negative.");
            }
        }

        var expectedTotal = decimal.Round(
            message.Items.Sum(item => item.UnitPrice * item.Quantity),
            2,
            MidpointRounding.AwayFromZero);

        if (message.Total < 0m || message.Total != expectedTotal)
        {
            errors.Add("The total does not match the sum of the items.");
        }

        return errors;
    }
}
