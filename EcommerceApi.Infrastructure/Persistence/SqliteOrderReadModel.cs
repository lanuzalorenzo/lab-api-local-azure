using System.Globalization;
using EcommerceApi.Application.Abstractions;
using EcommerceApi.Application.Orders;
using Microsoft.Data.Sqlite;

namespace EcommerceApi.Infrastructure.Persistence;

public sealed class SqliteOrderReadModel : IOrderReadModel
{
    private readonly SqliteSession _session;

    public SqliteOrderReadModel(SqliteSession session) => _session = session;

    public async Task<IReadOnlyList<OrderSummary>> GetByUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetConnectionAsync(cancellationToken);

        var orders = new List<OrderSummary>();

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = _session.Transaction;
            command.CommandText = """
                SELECT Id, CreatedAt, TotalCents
                FROM Orders
                WHERE UserId = $userId
                ORDER BY Id;
                """;
            command.Parameters.AddWithValue("$userId", userId);

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

        if (orders.Count == 0)
        {
            return orders;
        }

        var lines = await LoadLinesAsync(
            connection,
            """
            SELECT OrderId, ProductId
            FROM OrderLines
            WHERE OrderId IN (SELECT Id FROM Orders WHERE UserId = $userId)
            ORDER BY OrderId, ProductId;
            """,
            "$userId",
            userId,
            cancellationToken);

        return orders
            .Select(order => order with { ProductIds = lines.GetValueOrDefault(order.Id) ?? [] })
            .ToList();
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

        var lines = await LoadLinesAsync(
            connection,
            "SELECT OrderId, ProductId FROM OrderLines WHERE OrderId = $orderId ORDER BY ProductId;",
            "$orderId",
            order.Id,
            cancellationToken);

        return order with { ProductIds = lines.GetValueOrDefault(order.Id) ?? [] };
    }

    private async Task<Dictionary<int, List<int>>> LoadLinesAsync(
        SqliteConnection connection,
        string sql,
        string parameterName,
        object parameterValue,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = _session.Transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue(parameterName, parameterValue);

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

    private static DateTime ParseDate(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
