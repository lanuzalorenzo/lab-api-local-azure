using System.Collections.Concurrent;
using EcommerceApi.Models;

namespace EcommerceApi.Services;

/// <summary>
/// Thread-safe, in-memory repository used by the lab. It is intentionally
/// ephemeral: restarting the process resets products and orders.
/// </summary>
public sealed class InMemoryStore
{
    private readonly ConcurrentDictionary<int, Product> _products = new();
    private readonly ConcurrentDictionary<int, Order> _orders = new();
    private int _nextProductId;
    private int _nextOrderId;

    public InMemoryStore()
    {
        AddProduct(new Product { Name = "Laptop", Price = 999.99m, Stock = 10 });
        AddProduct(new Product { Name = "Mouse", Price = 29.99m, Stock = 50 });
        AddProduct(new Product { Name = "Keyboard", Price = 79.99m, Stock = 30 });
        AddProduct(new Product { Name = "Monitor", Price = 299.99m, Stock = 15 });
        AddProduct(new Product { Name = "USB Cable", Price = 9.99m, Stock = 100 });
    }

    public IReadOnlyList<Product> GetProducts() =>
        _products.Values.OrderBy(product => product.Id).ToList();

    public Product? GetProduct(int id) =>
        _products.TryGetValue(id, out var product) ? product : null;

    public Product AddProduct(Product product)
    {
        product.Id = Interlocked.Increment(ref _nextProductId);
        _products[product.Id] = product;
        return product;
    }

    public IReadOnlyList<Order> GetOrders(string userId) =>
        _orders.Values
            .Where(order => string.Equals(order.UserId, userId, StringComparison.Ordinal))
            .OrderBy(order => order.Id)
            .ToList();

    public Order? GetOrder(int id, string userId) =>
        _orders.TryGetValue(id, out var order) &&
        string.Equals(order.UserId, userId, StringComparison.Ordinal)
            ? order
            : null;

    public Order AddOrder(Order order)
    {
        order.Id = Interlocked.Increment(ref _nextOrderId);
        _orders[order.Id] = order;
        return order;
    }

    public bool AllProductsExist(IEnumerable<int> productIds) =>
        productIds.All(_products.ContainsKey);

    public decimal CalculateTotal(IEnumerable<int> productIds) =>
        productIds.Sum(productId => _products[productId].Price);
}
