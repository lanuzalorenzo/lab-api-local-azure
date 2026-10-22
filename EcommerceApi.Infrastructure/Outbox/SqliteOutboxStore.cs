using System.Globalization;
using EcommerceApi.Application.Abstractions;
using EcommerceApi.Application.Outbox;
using EcommerceApi.Infrastructure.Persistence;

namespace EcommerceApi.Infrastructure.Outbox;

/// <summary>
/// SQLite implementation of the transactional outbox. Writes enlist in the session
/// transaction opened by the order handler, so the order and its message commit
/// together.
/// </summary>
public sealed class SqliteOutboxStore : IOutboxWriter, IOutboxStore
{
    private readonly SqliteSession _session;

    public SqliteOutboxStore(SqliteSession session) => _session = session;

    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.Transaction = _session.Transaction;
        command.CommandText = """
            INSERT INTO OutboxMessages (MessageId, Type, Payload, OccurredAt)
            VALUES ($messageId, $type, $payload, $occurredAt);
            """;
        command.Parameters.AddWithValue("$messageId", message.MessageId);
        command.Parameters.AddWithValue("$type", message.Type);
        command.Parameters.AddWithValue("$payload", message.Payload);
        command.Parameters.AddWithValue(
            "$occurredAt",
            message.OccurredAt.ToString("O", CultureInfo.InvariantCulture));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PendingOutboxMessage>> GetPendingAsync(
        int maxMessages,
        CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.Transaction = _session.Transaction;
        command.CommandText = """
            SELECT Id, MessageId, Type, Payload, OccurredAt, Attempts
            FROM OutboxMessages
            WHERE ProcessedAt IS NULL AND DeadLetteredAt IS NULL
            ORDER BY Id
            LIMIT $max;
            """;
        command.Parameters.AddWithValue("$max", maxMessages);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var pending = new List<PendingOutboxMessage>();

        while (await reader.ReadAsync(cancellationToken))
        {
            pending.Add(new PendingOutboxMessage(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                DateTime.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                reader.GetInt32(5)));
        }

        return pending;
    }

    public async Task MarkProcessedAsync(
        long id,
        DateTime processedAt,
        CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.Transaction = _session.Transaction;
        command.CommandText = """
            UPDATE OutboxMessages
            SET ProcessedAt = $processedAt, LastError = NULL
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue(
            "$processedAt",
            processedAt.ToString("O", CultureInfo.InvariantCulture));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(
        long id,
        int attempts,
        string error,
        bool deadLetter,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.Transaction = _session.Transaction;
        command.CommandText = """
            UPDATE OutboxMessages
            SET Attempts = $attempts,
                LastError = $error,
                DeadLetteredAt = CASE WHEN $deadLetter = 1 THEN $now ELSE DeadLetteredAt END
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$attempts", attempts);
        command.Parameters.AddWithValue("$error", error);
        command.Parameters.AddWithValue("$deadLetter", deadLetter ? 1 : 0);
        command.Parameters.AddWithValue("$now", now.ToString("O", CultureInfo.InvariantCulture));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
