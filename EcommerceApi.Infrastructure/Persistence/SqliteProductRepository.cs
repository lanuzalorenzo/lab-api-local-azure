using EcommerceApi.Application.Abstractions;
using EcommerceApi.Domain.Products;

namespace EcommerceApi.Infrastructure.Persistence;

public sealed class SqliteProductRepository : IProductRepository
{
    private const string SelectColumns = "Id, Name, PriceCents, Stock";

    private readonly SqliteSession _session;

    public SqliteProductRepository(SqliteSession session) => _session = session;

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.Transaction = _session.Transaction;
        command.CommandText = $"SELECT {SelectColumns} FROM Products ORDER BY Id;";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var products = new List<Product>();

        while (await reader.ReadAsync(cancellationToken))
        {
            products.Add(ReadProduct(reader));
        }

        return products;
    }

    public async Task<Product?> FindAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.Transaction = _session.Transaction;
        command.CommandText = $"SELECT {SelectColumns} FROM Products WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? ReadProduct(reader) : null;
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.Transaction = _session.Transaction;
        command.CommandText = """
            INSERT INTO Products (Name, PriceCents, Stock)
            VALUES ($name, $priceCents, $stock);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$name", product.Name);
        command.Parameters.AddWithValue("$priceCents", MoneyCents.ToCents(product.Price.Amount));
        command.Parameters.AddWithValue("$stock", product.Stock);

        var id = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        product.AssignIdentity(id);
    }

    private static Product ReadProduct(Microsoft.Data.Sqlite.SqliteDataReader reader) =>
        Product.Rehydrate(
            reader.GetInt32(0),
            reader.GetString(1),
            MoneyCents.FromCents(reader.GetInt64(2)),
            reader.GetInt32(3));
}
