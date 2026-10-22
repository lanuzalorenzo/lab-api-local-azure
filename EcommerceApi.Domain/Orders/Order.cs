using EcommerceApi.Domain.Common;

namespace EcommerceApi.Domain.Orders;

/// <summary>
/// Order aggregate root. It owns its lines, enforces the order invariants and is
/// the only entry point for creating or rehydrating an order.
/// </summary>
public sealed class Order
{
    public const int MaxLines = 20;

    private readonly List<OrderLine> _lines = new();
    private readonly List<IDomainEvent> _domainEvents = new();

    private Order(int id, string userId, DateTime createdAt, IEnumerable<OrderLine> lines)
    {
        Id = id;
        UserId = userId;
        CreatedAt = createdAt;
        _lines.AddRange(lines);
    }

    public int Id { get; private set; }

    public string UserId { get; }

    public DateTime CreatedAt { get; }

    public IReadOnlyList<OrderLine> Lines => _lines;

    public IReadOnlyList<int> ProductIds => _lines.Select(line => line.ProductId).ToList();

    public Money Total => _lines.Aggregate(Money.Zero, (total, line) => total + line.LineTotal);

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    /// <summary>
    /// Creates a new order and raises <see cref="OrderPlacedDomainEvent"/>. The
    /// identity is assigned later by the repository through <see cref="AssignIdentity"/>.
    /// </summary>
    public static Order Create(string userId, IEnumerable<OrderLine> lines, DateTime createdAt)
    {
        var materialized = ValidateAndMaterialize(userId, lines);

        var order = new Order(0, userId.Trim(), createdAt, materialized);
        order.Raise(new OrderPlacedDomainEvent(order, createdAt));

        return order;
    }

    /// <summary>
    /// Rebuilds an existing order from persistence without raising events.
    /// </summary>
    public static Order Rehydrate(int id, string userId, DateTime createdAt, IEnumerable<OrderLine> lines)
    {
        if (id <= 0)
        {
            throw new DomainException("A persisted order must have a positive identifier.");
        }

        var materialized = ValidateAndMaterialize(userId, lines);

        return new Order(id, userId.Trim(), createdAt, materialized);
    }

    public void AssignIdentity(int id)
    {
        if (id <= 0)
        {
            throw new DomainException("An order identifier must be positive.");
        }

        if (Id != 0)
        {
            throw new DomainException("The order already has an identity.");
        }

        Id = id;
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    private static List<OrderLine> ValidateAndMaterialize(string userId, IEnumerable<OrderLine> lines)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new DomainException("An order must belong to a user.");
        }

        ArgumentNullException.ThrowIfNull(lines);

        var materialized = lines.ToList();

        if (materialized.Count == 0)
        {
            throw new DomainException("An order must contain at least one line.");
        }

        if (materialized.Count > MaxLines)
        {
            throw new DomainException($"An order cannot contain more than {MaxLines} lines.");
        }

        if (materialized.Select(line => line.ProductId).Distinct().Count() != materialized.Count)
        {
            throw new DomainException("An order cannot contain duplicate products.");
        }

        return materialized;
    }

    private void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
