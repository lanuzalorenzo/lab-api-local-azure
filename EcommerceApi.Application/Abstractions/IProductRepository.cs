using EcommerceApi.Domain.Products;

namespace EcommerceApi.Application.Abstractions;

/// <summary>
/// Outbound port for the product catalogue. The application layer depends on this
/// abstraction, never on the SQLite (or future) infrastructure implementation.
/// </summary>
public interface IProductRepository
{
    Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Product?> FindAsync(int id, CancellationToken cancellationToken = default);

    Task AddAsync(Product product, CancellationToken cancellationToken = default);
}
