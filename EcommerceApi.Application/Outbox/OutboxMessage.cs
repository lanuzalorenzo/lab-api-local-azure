namespace EcommerceApi.Application.Outbox;

/// <summary>
/// A message waiting to be published. It is written in the same transaction as the
/// business change, so a crash cannot leave a committed order without its event.
/// </summary>
public sealed record OutboxMessage(string MessageId, string Type, string Payload, DateTime OccurredAt);
