using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace EcommerceApi.Infrastructure.Persistence;

/// <summary>
/// Creates SQLite connections from configuration. Foreign keys are enabled on every
/// connection because SQLite disables them by default.
/// </summary>
public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(IOptions<StorageOptions> options) =>
        _connectionString = options.Value.ConnectionString;

    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        await command.ExecuteNonQueryAsync(cancellationToken);

        return connection;
    }
}
