using System.Globalization;
using System.Text;
using EcommerceApi.Application.Abstractions;
using EcommerceApi.Application.Orders;
using Microsoft.Data.Sqlite;

namespace EcommerceApi.Infrastructure.Persistence;

/// <summary>
/// Read-side adapter over SQLite. It uses explicit SQL and keeps the list query
/// bounded: one page of orders plus one query for the lines of that page.
/// </summary>
public sealed class SqliteOrderReadModel : IOrderReadModel
{
    private readonly SqliteSession _session;

    public SqliteOrderReadModel(SqliteSession session) => _session = session;

    public async Task<OrderPage> GetByUserAsync(
        string userId,
        OrderQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Limit is < 1 or > OrderQuery.MaxLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                $"Limit must be between 1 and {OrderQuery.MaxLimit}.");
        }

        if (query.AfterId is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "AfterId cannot be negative.");
        }

        if (query.From is not null && query.To is not null && query.From >= query.To)
        {
            throw new ArgumentException("From must be earlier than To.", nameof(query));
        }

        var connection = await _session.GetConnectionAsync(cancellationToken);
        var orders = new List<OrderSummary>();

        var sql = new StringBuilder("""
            SELECT Id, CreatedAt, TotalCents
            FROM Orders
            WHERE UserId = $userId
            """);

        if (query.AfterId is not null)
        {
            sql.AppendLine();
            sql.Append("  AND Id > $afterId");
        }

        if (query.From is not null)
        {
            sql.AppendLine();
            sql.Append("  AND CreatedAt >= $from");
        }

        if (query.To is not null)
        {
            sql.AppendLine();
            sql.Append("  AND CreatedAt < $to");
        }

        sql.AppendLine();
        sql.Append("ORDER BY Id");
        sql.AppendLine();
        sql.Append("LIMIT $limit;");

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = _session.Transaction;
            command.CommandText = sql.ToString();
            command.Parameters.AddWithValue("$userId", userId);
            command.Parameters.AddWithValue("$limit", query.Limit + 1);

            if (query.AfterId is not null)
            {
                command.Parameters.AddWithValue("$afterId", query.AfterId.Value);
            }

            if (query.From is not null)
            {
                command.Parameters.AddWithValue("$from", FormatDate(query.From.Value));
            }

            if (query.To is not null)
            {
                command.Parameters.AddWithValue("$to", FormatDate(query.To.Value));
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                orders.Add(new OrderSummary(
                    reader.GetInt32(0),
                    ParseDate(reader.GetString(1)),
                    [],
                    MoneyCents.FromCents(reader.GetInt64(2))));
            }
        }

        int? nextCursor = null;
        if (orders.Count > query.Limit)
        {
            orders.RemoveAt(orders.Count - 1);
            nextCursor = orders[^1].Id;
        }

        if (orders.Count == 0)
        {
            return new OrderPage(orders, null);
        }

        var lines = await LoadLinesAsync(
            connection,
            orders.Select(order => order.Id).ToList(),
            cancellationToken);

        var items = orders
            .Select(order => order with { ProductIds = lines.GetValueOrDefault(order.Id) ?? [] })
            .ToList();

        return new OrderPage(items, nextCursor);
    }

    public async Task<OrderSummary?> FindAsync(
        int id,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetConnectionAsync(cancellationToken);

        OrderSummary? order;

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = _session.Transaction;
            command.CommandText = """
                SELECT Id, CreatedAt, TotalCents
                FROM Orders
                WHERE Id = $id AND UserId = $userId;
                """;
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$userId", userId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            order = new OrderSummary(
                reader.GetInt32(0),
                ParseDate(reader.GetString(1)),
                [],
                MoneyCents.FromCents(reader.GetInt64(2)));
        }

        var lines = await LoadLinesAsync(connection, [order.Id], cancellationToken);

        return order with { ProductIds = lines.GetValueOrDefault(order.Id) ?? [] };
    }

    private async Task<Dictionary<int, List<int>>> LoadLinesAsync(
        SqliteConnection connection,
        IReadOnlyList<int> orderIds,
        CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0)
        {
            return [];
        }

        var parameterNames = string.Join(
            ", ",
            orderIds.Select((_, index) => $"$orderId{index}"));

        await using var command = connection.CreateCommand();
        command.Transaction = _session.Transaction;
        command.CommandText = $"""
            SELECT OrderId, ProductId
            FROM OrderLines
            WHERE OrderId IN ({parameterNames})
            ORDER BY OrderId, ProductId;
            """;

        for (var index = 0; index < orderIds.Count; index++)
        {
            command.Parameters.AddWithValue($"$orderId{index}", orderIds[index]);
        }

        var lines = new Dictionary<int, List<int>>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var orderId = reader.GetInt32(0);
            var productId = reader.GetInt32(1);

            if (!lines.TryGetValue(orderId, out var productIds))
            {
                productIds = [];
                lines[orderId] = productIds;
            }

            productIds.Add(productId);
        }

        return lines;
    }

    private static string FormatDate(DateTimeOffset value) =>
        value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private static DateTime ParseDate(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
