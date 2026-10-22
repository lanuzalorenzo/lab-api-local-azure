using System.Collections.Concurrent;
using EcommerceApi.Application.Abstractions;
using EcommerceApi.Domain.Products;

namespace EcommerceApi.Infrastructure.Persistence;

/// <summary>
/// Ephemeral, thread-safe implementation of the product port. It exists so the lab
/// can run without a database; replacing it with a persistent adapter must not
/// require changes in the application layer.
/// </summary>
public sealed class InMemoryProductRepository : IProductRepository
{
    private readonly ConcurrentDictionary<int, Product> _products = new();
    private int _nextId;

    public InMemoryProductRepository()
    {
        Add(Product.Create("Laptop", 999.99m, 10));
        Add(Product.Create("Mouse", 29.99m, 50));
        Add(Product.Create("Keyboard", 79.99m, 30));
        Add(Product.Create("Monitor", 299.99m, 15));
        Add(Product.Create("USB Cable", 9.99m, 100));
    }

    public IReadOnlyList<Product> GetAll() =>
        _products.Values.OrderBy(product => product.Id).ToList();

    public Product? Find(int id) =>
        _products.TryGetValue(id, out var product) ? product : null;

    public Product Add(Product product)
    {
        product.AssignIdentity(Interlocked.Increment(ref _nextId));
        _products[product.Id] = product;
        return product;
    }
}
