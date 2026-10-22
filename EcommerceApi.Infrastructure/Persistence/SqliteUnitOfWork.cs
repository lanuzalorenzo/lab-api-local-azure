using EcommerceApi.Application.Abstractions;

namespace EcommerceApi.Infrastructure.Persistence;

public sealed class SqliteUnitOfWork : IUnitOfWork
{
    private readonly SqliteSession _session;

    public SqliteUnitOfWork(SqliteSession session) => _session = session;

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        await _session.BeginTransactionAsync(cancellationToken);

        return new Transaction(_session);
    }

    private sealed class Transaction : IUnitOfWorkTransaction
    {
        private readonly SqliteSession _session;
        private bool _completed;

        public Transaction(SqliteSession session) => _session = session;

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await _session.CommitAsync(cancellationToken);
            _completed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_completed)
            {
                await _session.RollbackAsync();
            }
        }
    }
}
