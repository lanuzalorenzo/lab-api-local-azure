using System.Globalization;
using EcommerceApi.Application.Abstractions;
using EcommerceApi.Domain.Orders;

namespace EcommerceApi.Infrastructure.Persistence;

public sealed class SqliteOrderRepository : IOrderRepository
{
    private readonly SqliteSession _session;

    public SqliteOrderRepository(SqliteSession session) => _session = session;

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetConnectionAsync(cancellationToken);

        await using var insertOrder = connection.CreateCommand();
        insertOrder.Transaction = _session.Transaction;
        insertOrder.CommandText = """
            INSERT INTO Orders (UserId, CreatedAt, TotalCents)
            VALUES ($userId, $createdAt, $totalCents);
            SELECT last_insert_rowid();
            """;
        insertOrder.Parameters.AddWithValue("$userId", order.UserId);
        insertOrder.Parameters.AddWithValue(
            "$createdAt",
            order.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        insertOrder.Parameters.AddWithValue("$totalCents", MoneyCents.ToCents(order.Total.Amount));

        var id = Convert.ToInt32(await insertOrder.ExecuteScalarAsync(cancellationToken));
        order.AssignIdentity(id);

        foreach (var line in order.Lines)
        {
            await using var insertLine = connection.CreateCommand();
            insertLine.Transaction = _session.Transaction;
            insertLine.CommandText = """
                INSERT INTO OrderLines (OrderId, ProductId, UnitPriceCents, Quantity)
                VALUES ($orderId, $productId, $unitPriceCents, $quantity);
                """;
            insertLine.Parameters.AddWithValue("$orderId", order.Id);
            insertLine.Parameters.AddWithValue("$productId", line.ProductId);
            insertLine.Parameters.AddWithValue("$unitPriceCents", MoneyCents.ToCents(line.UnitPrice.Amount));
            insertLine.Parameters.AddWithValue("$quantity", line.Quantity);

            await insertLine.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
