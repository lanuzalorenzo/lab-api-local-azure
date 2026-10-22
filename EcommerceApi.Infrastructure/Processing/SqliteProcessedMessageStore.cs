using System.Globalization;
using EcommerceApi.Application.Abstractions;
using EcommerceApi.Infrastructure.Persistence;

namespace EcommerceApi.Infrastructure.Processing;

/// <summary>
/// SQLite idempotency store for the consumer. In Azure this would be Table Storage
/// or SQL; the contract is the same.
/// </summary>
public sealed class SqliteProcessedMessageStore : IProcessedMessageStore
{
    private readonly SqliteSession _session;

    public SqliteProcessedMessageStore(SqliteSession session) => _session = session;

    public async Task<bool> ExistsAsync(string messageId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.Transaction = _session.Transaction;
        command.CommandText = "SELECT 1 FROM ProcessedMessages WHERE MessageId = $messageId LIMIT 1;";
        command.Parameters.AddWithValue("$messageId", messageId);

        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    public async Task MarkProcessedAsync(
        string messageId,
        DateTime processedAt,
        CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.Transaction = _session.Transaction;
        command.CommandText = """
            INSERT INTO ProcessedMessages (MessageId, ProcessedAt)
            VALUES ($messageId, $processedAt)
            ON CONFLICT (MessageId) DO NOTHING;
            """;
        command.Parameters.AddWithValue("$messageId", messageId);
        command.Parameters.AddWithValue(
            "$processedAt",
            processedAt.ToString("O", CultureInfo.InvariantCulture));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
