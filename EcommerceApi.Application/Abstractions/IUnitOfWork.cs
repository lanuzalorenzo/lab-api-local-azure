namespace EcommerceApi.Application.Abstractions;

/// <summary>
/// Explicit transaction boundary. The handler owns it, so the order and its outbox
/// message are committed together or not at all.
/// </summary>
public interface IUnitOfWork
{
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
