namespace EcommerceApi.Application.Products;

/// <summary>
/// Read model for the catalogue. It is intentionally flat: the API never exposes
/// the product aggregate directly.
/// </summary>
public sealed record ProductView(int Id, string Name, decimal Price, int Stock);
