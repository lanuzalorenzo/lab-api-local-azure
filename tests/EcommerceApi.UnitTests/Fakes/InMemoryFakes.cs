using EcommerceApi.Application.Abstractions;
using EcommerceApi.Application.Orders;
using EcommerceApi.Application.Outbox;
using EcommerceApi.Domain.Orders;
using EcommerceApi.Domain.Products;

namespace EcommerceApi.UnitTests.Fakes;

internal sealed class FakeProductRepository : IProductRepository
{
    private readonly List<Product> _items = [];
    private int _nextId;

    public FakeProductRepository(params Product[] products)
    {
        foreach (var product in products)
        {
            AddAsync(product).GetAwaiter().GetResult();
        }
    }

    public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Product>>(_items.OrderBy(product => product.Id).ToList());

    public Task<Product?> FindAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_items.FirstOrDefault(product => product.Id == id));

    public Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        product.AssignIdentity(++_nextId);
        _items.Add(product);
        return Task.CompletedTask;
    }
}

internal sealed class FakeOrderRepository : IOrderRepository
{
    private readonly List<Order> _items = [];
    private int _nextId;

    public IReadOnlyList<Order> Items => _items;

    public Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        order.AssignIdentity(++_nextId);
        _items.Add(order);
        return Task.CompletedTask;
    }
}

internal sealed class FakeOrderReadModel : IOrderReadModel
{
    private readonly Dictionary<string, List<OrderSummary>> _byUser = new(StringComparer.Ordinal);

    public OrderQuery? LastQuery { get; private set; }

    public void Add(string userId, OrderSummary summary)
    {
        if (!_byUser.TryGetValue(userId, out var summaries))
        {
            summaries = [];
            _byUser[userId] = summaries;
        }

        summaries.Add(summary);
    }

    public Task<OrderPage> GetByUserAsync(
        string userId,
        OrderQuery query,
        CancellationToken cancellationToken = default)
    {
        LastQuery = query;

        var page = (_byUser.GetValueOrDefault(userId) ?? [])
            .Where(order => query.AfterId is null || order.Id > query.AfterId)
            .Where(order => query.From is null || order.CreatedAt >= query.From.Value.UtcDateTime)
            .Where(order => query.To is null || order.CreatedAt < query.To.Value.UtcDateTime)
            .OrderBy(order => order.Id)
            .Take(query.Limit + 1)
            .ToList();

        int? nextCursor = null;
        if (page.Count > query.Limit)
        {
            page.RemoveAt(page.Count - 1);
            nextCursor = page[^1].Id;
        }

        return Task.FromResult(new OrderPage(page, nextCursor));
    }

    public Task<OrderSummary?> FindAsync(
        int id,
        string userId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_byUser.GetValueOrDefault(userId)?.FirstOrDefault(order => order.Id == id));
}

internal sealed class FakeOutboxWriter : IOutboxWriter
{
    public List<OutboxMessage> Messages { get; } = [];

    public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public bool Committed { get; private set; }

    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IUnitOfWorkTransaction>(new Transaction(this));

    private sealed class Transaction : IUnitOfWorkTransaction
    {
        private readonly FakeUnitOfWork _unitOfWork;

        public Transaction(FakeUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            _unitOfWork.Committed = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

internal sealed class FakeProcessedMessageStore : IProcessedMessageStore
{
    private readonly HashSet<string> _processed = new(StringComparer.Ordinal);

    public int MarkProcessedCalls { get; private set; }

    public Task<bool> ExistsAsync(string messageId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_processed.Contains(messageId));

    public Task MarkProcessedAsync(
        string messageId,
        DateTime processedAt,
        CancellationToken cancellationToken = default)
    {
        MarkProcessedCalls++;
        _processed.Add(messageId);
        return Task.CompletedTask;
    }
}

internal sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;

    public FixedTimeProvider(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now;
}
