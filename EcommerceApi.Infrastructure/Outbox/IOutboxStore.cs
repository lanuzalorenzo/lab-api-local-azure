namespace EcommerceApi.Infrastructure.Outbox;

/// <summary>
/// A pending outbox row, including the delivery attempt counter. Kept in the
/// infrastructure layer because it models storage state, not a domain concept.
/// </summary>
public sealed record PendingOutboxMessage(
    long Id,
    string MessageId,
    string Type,
    string Payload,
    DateTime OccurredAt,
    int Attempts);

/// <summary>
/// Storage operations used by the dispatcher. The write side lives behind the
/// application port <c>IOutboxWriter</c>; this interface adds the delivery state.
/// </summary>
public interface IOutboxStore
{
    Task<IReadOnlyList<PendingOutboxMessage>> GetPendingAsync(
        int maxMessages,
        CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(long id, DateTime processedAt, CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        long id,
        int attempts,
        string error,
        bool deadLetter,
        DateTime now,
        CancellationToken cancellationToken = default);
}
