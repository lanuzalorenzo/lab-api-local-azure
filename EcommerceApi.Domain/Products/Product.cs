using EcommerceApi.Domain.Common;

namespace EcommerceApi.Domain.Products;

/// <summary>
/// Product aggregate root. It protects its own invariants (name, price, stock) so
/// that no caller can persist an inconsistent catalogue entry.
/// </summary>
public sealed class Product
{
    public const int NameMaxLength = 100;
    public const decimal MaxPrice = 1_000_000m;
    public const int MaxStock = 1_000_000;

    private Product(int id, string name, Money price, int stock)
    {
        Id = id;
        Name = name;
        Price = price;
        Stock = stock;
    }

    public int Id { get; private set; }

    public string Name { get; private set; }

    public Money Price { get; private set; }

    public int Stock { get; private set; }

    public static Product Create(string name, decimal price, int stock) =>
        new(0, NormalizeName(name), ValidatePrice(price), ValidateStock(stock));

    public static Product Rehydrate(int id, string name, decimal price, int stock)
    {
        if (id <= 0)
        {
            throw new DomainException("A persisted product must have a positive identifier.");
        }

        return new Product(id, NormalizeName(name), ValidatePrice(price), ValidateStock(stock));
    }

    public void AssignIdentity(int id)
    {
        if (id <= 0)
        {
            throw new DomainException("A product identifier must be positive.");
        }

        if (Id != 0)
        {
            throw new DomainException("The product already has an identity.");
        }

        Id = id;
    }

    public void Rename(string name) => Name = NormalizeName(name);

    public void ChangePrice(decimal price) => Price = ValidatePrice(price);

    public void ChangeStock(int stock) => Stock = ValidateStock(stock);

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("A product must have a name.");
        }

        var trimmed = name.Trim();

        if (trimmed.Length > NameMaxLength)
        {
            throw new DomainException($"A product name cannot exceed {NameMaxLength} characters.");
        }

        return trimmed;
    }

    private static Money ValidatePrice(decimal price)
    {
        if (price > MaxPrice)
        {
            throw new DomainException($"A product price cannot exceed {MaxPrice}.");
        }

        return Money.From(price);
    }

    private static int ValidateStock(int stock)
    {
        if (stock < 0 || stock > MaxStock)
        {
            throw new DomainException($"A product stock must be between 0 and {MaxStock}.");
        }

        return stock;
    }
}
