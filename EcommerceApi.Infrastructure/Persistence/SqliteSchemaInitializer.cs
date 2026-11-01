using Microsoft.Data.Sqlite;

namespace EcommerceApi.Infrastructure.Persistence;

/// <summary>
/// Creates the schema and seeds the catalogue once. It runs at startup, before the
/// host starts accepting requests, so no request can observe a half-built database.
/// </summary>
public sealed class SqliteSchemaInitializer
{
    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS Products (
            Id         INTEGER PRIMARY KEY AUTOINCREMENT,
            Name       TEXT    NOT NULL,
            PriceCents INTEGER NOT NULL,
            Stock      INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Orders (
            Id         INTEGER PRIMARY KEY AUTOINCREMENT,
            UserId     TEXT    NOT NULL,
            CreatedAt  TEXT    NOT NULL,
            TotalCents INTEGER NOT NULL
        );

        -- Keyset pagination filters by owner and resumes after the last identifier.
        -- The composite index also covers queries that only filter by UserId.
        DROP INDEX IF EXISTS IX_Orders_UserId;
        CREATE INDEX IF NOT EXISTS IX_Orders_UserId_Id ON Orders (UserId, Id);

        CREATE TABLE IF NOT EXISTS OrderLines (
            OrderId        INTEGER NOT NULL,
            ProductId      INTEGER NOT NULL,
            UnitPriceCents INTEGER NOT NULL,
            Quantity       INTEGER NOT NULL,
            PRIMARY KEY (OrderId, ProductId),
            FOREIGN KEY (OrderId) REFERENCES Orders (Id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS OutboxMessages (
            Id             INTEGER PRIMARY KEY AUTOINCREMENT,
            MessageId      TEXT    NOT NULL UNIQUE,
            Type           TEXT    NOT NULL,
            Payload        TEXT    NOT NULL,
            OccurredAt     TEXT    NOT NULL,
            ProcessedAt    TEXT    NULL,
            DeadLetteredAt TEXT    NULL,
            Attempts       INTEGER NOT NULL DEFAULT 0,
            LastError      TEXT    NULL
        );

        CREATE INDEX IF NOT EXISTS IX_OutboxMessages_Pending
            ON OutboxMessages (Id)
            WHERE ProcessedAt IS NULL AND DeadLetteredAt IS NULL;

        CREATE TABLE IF NOT EXISTS ProcessedMessages (
            MessageId   TEXT PRIMARY KEY,
            ProcessedAt TEXT NOT NULL
        );
        """;

    private readonly SqliteConnectionFactory _factory;

    public SqliteSchemaInitializer(SqliteConnectionFactory factory) => _factory = factory;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _factory.OpenAsync(cancellationToken);

        await using (var schema = connection.CreateCommand())
        {
            schema.CommandText = SchemaSql;
            await schema.ExecuteNonQueryAsync(cancellationToken);
        }

        await SeedProductsAsync(connection, cancellationToken);
    }

    private static async Task SeedProductsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM Products;";
        var count = Convert.ToInt64(await countCommand.ExecuteScalarAsync(cancellationToken));

        if (count > 0)
        {
            return;
        }

        await using var transaction = connection.BeginTransaction();
        await using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT INTO Products (Name, PriceCents, Stock) VALUES
                ('Laptop', 99999, 10),
                ('Mouse', 2999, 50),
                ('Keyboard', 7999, 30),
                ('Monitor', 29999, 15),
                ('USB Cable', 999, 100);
            """;

        await insert.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
