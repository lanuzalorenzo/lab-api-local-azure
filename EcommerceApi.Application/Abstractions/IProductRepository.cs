using EcommerceApi.Domain.Products;

namespace EcommerceApi.Application.Abstractions;

/// <summary>
/// Outbound port for the product catalogue. The application layer depends on this
/// abstraction, never on the in-memory (or future) infrastructure implementation.
/// </summary>
public interface IProductRepository
{
    IReadOnlyList<Product> GetAll();

    Product? Find(int id);

    Product Add(Product product);
}
