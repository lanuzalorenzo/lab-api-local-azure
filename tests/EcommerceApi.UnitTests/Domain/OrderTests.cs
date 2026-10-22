using EcommerceApi.Domain.Common;
using EcommerceApi.Domain.Orders;
using Xunit;

namespace EcommerceApi.UnitTests.Domain;

public sealed class OrderTests
{
    private static readonly DateTime CreatedAt = new(2026, 10, 22, 10, 0, 0, DateTimeKind.Utc);

    private static OrderLine Line(int productId, decimal price = 10m, int quantity = 1) =>
        new(productId, Money.From(price), quantity);

    [Fact]
    public void Create_WithoutLines_Throws()
    {
        Assert.Throws<DomainException>(() =>
            Order.Create("user-1", Array.Empty<OrderLine>(), CreatedAt));
    }

    [Fact]
    public void Create_WithDuplicateProduct_Throws()
    {
        Assert.Throws<DomainException>(() =>
            Order.Create("user-1", [Line(1), Line(1)], CreatedAt));
    }

    [Fact]
    public void Create_WithBlankUser_Throws()
    {
        Assert.Throws<DomainException>(() =>
            Order.Create("  ", [Line(1)], CreatedAt));
    }

    [Fact]
    public void Create_WithMoreThanMaxLines_Throws()
    {
        var lines = Enumerable.Range(1, Order.MaxLines + 1).Select(id => Line(id)).ToArray();

        Assert.Throws<DomainException>(() =>
            Order.Create("user-1", lines, CreatedAt));
    }

    [Fact]
    public void Create_ComputesTotalFromLines()
    {
        var order = Order.Create("user-1", [Line(1, 10.00m), Line(2, 5.50m)], CreatedAt);

        Assert.Equal(15.50m, order.Total.Amount);
    }

    [Fact]
    public void Create_PreservesLineOrderAndProductIds()
    {
        var order = Order.Create("user-1", [Line(3), Line(1), Line(2)], CreatedAt);

        Assert.Equal([3, 1, 2], order.ProductIds);
    }

    [Fact]
    public void Create_RaisesOrderPlacedEvent()
    {
        var order = Order.Create("user-1", [Line(1)], CreatedAt);

        var domainEvent = Assert.Single(order.DomainEvents);
        var placed = Assert.IsType<OrderPlacedDomainEvent>(domainEvent);
        Assert.Equal("user-1", placed.UserId);
        Assert.Equal(10.00m, placed.Total);
    }

    [Fact]
    public void Create_DoesNotAssignIdentity()
    {
        var order = Order.Create("user-1", [Line(1)], CreatedAt);

        Assert.Equal(0, order.Id);
    }

    [Fact]
    public void AssignIdentity_SetsIdOnce()
    {
        var order = Order.Create("user-1", [Line(1)], CreatedAt);

        order.AssignIdentity(42);

        Assert.Equal(42, order.Id);
        Assert.Throws<DomainException>(() => order.AssignIdentity(43));
    }

    [Fact]
    public void ClearDomainEvents_RemovesRaisedEvents()
    {
        var order = Order.Create("user-1", [Line(1)], CreatedAt);

        order.ClearDomainEvents();

        Assert.Empty(order.DomainEvents);
    }

    [Fact]
    public void Rehydrate_DoesNotRaiseEvents()
    {
        var order = Order.Rehydrate(7, "user-1", CreatedAt, [Line(1)]);

        Assert.Equal(7, order.Id);
        Assert.Empty(order.DomainEvents);
    }

    [Fact]
    public void OrderLine_WithNonPositiveProduct_Throws()
    {
        Assert.Throws<DomainException>(() => new OrderLine(0, Money.From(1m), 1));
    }

    [Fact]
    public void OrderLine_WithNonPositiveQuantity_Throws()
    {
        Assert.Throws<DomainException>(() => new OrderLine(1, Money.From(1m), 0));
    }
}
