using EcommerceApi.Application.Abstractions;
using EcommerceApi.Domain.Orders;
using EcommerceApi.Domain.Products;

namespace EcommerceApi.UnitTests.Fakes;

internal sealed class FakeProductRepository : IProductRepository
{
    private readonly Dictionary<int, Product> _items = new();
    private int _nextId;

    public FakeProductRepository(params Product[] products)
    {
        foreach (var product in products)
        {
            Add(product);
        }
    }

    public IReadOnlyList<Product> GetAll() => _items.Values.OrderBy(product => product.Id).ToList();

    public Product? Find(int id) => _items.TryGetValue(id, out var product) ? product : null;

    public Product Add(Product product)
    {
        product.AssignIdentity(++_nextId);
        _items[product.Id] = product;
        return product;
    }
}

internal sealed class FakeOrderRepository : IOrderRepository
{
    private readonly Dictionary<int, Order> _items = new();
    private int _nextId;

    public IReadOnlyList<Order> GetByUser(string userId) =>
        _items.Values
            .Where(order => order.UserId == userId)
            .OrderBy(order => order.Id)
            .ToList();

    public Order? Find(int id, string userId) =>
        _items.TryGetValue(id, out var order) && order.UserId == userId ? order : null;

    public Order Add(Order order)
    {
        order.AssignIdentity(++_nextId);
        _items[order.Id] = order;
        return order;
    }
}

internal sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;

    public FixedTimeProvider(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now;
}
