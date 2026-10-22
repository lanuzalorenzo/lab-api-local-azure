using Microsoft.Data.Sqlite;

namespace EcommerceApi.Infrastructure.Persistence;

/// <summary>
/// Scoped unit of persistence. Repositories and the outbox share the same connection
/// and transaction, which is what makes the transactional outbox work.
/// </summary>
public sealed class SqliteSession : IAsyncDisposable
{
    private readonly SqliteConnectionFactory _factory;
    private SqliteConnection? _connection;
    private SqliteTransaction? _transaction;

    public SqliteSession(SqliteConnectionFactory factory) => _factory = factory;

    public SqliteTransaction? Transaction => _transaction;

    public async Task<SqliteConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        _connection ??= await _factory.OpenAsync(cancellationToken);
        return _connection;
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            throw new InvalidOperationException("A transaction is already open in this session.");
        }

        var connection = await GetConnectionAsync(cancellationToken);
        _transaction = connection.BeginTransaction();
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        var transaction = _transaction
            ?? throw new InvalidOperationException("There is no open transaction to commit.");

        await transaction.CommitAsync(cancellationToken);
        await transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.RollbackAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}
