using EcommerceApi.Domain.Common;

namespace EcommerceApi.Domain.Orders;

/// <summary>
/// One product requested in an order, with the price captured at order time. It is
/// a value object: two lines with the same product, price and quantity are equal.
/// </summary>
public sealed record OrderLine
{
    public OrderLine(int productId, Money unitPrice, int quantity)
    {
        if (productId <= 0)
        {
            throw new DomainException("A product identifier must be positive.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("A line quantity must be greater than zero.");
        }

        ProductId = productId;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    public int ProductId { get; }

    public Money UnitPrice { get; }

    public int Quantity { get; }

    public Money LineTotal => UnitPrice.Multiply(Quantity);
}
