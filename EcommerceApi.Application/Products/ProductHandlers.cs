using EcommerceApi.Domain.Products;

namespace EcommerceApi.Application.Products;

public sealed record CreateProductCommand(string Name, decimal Price, int Stock);

public interface IGetProductsHandler
{
    Task<IReadOnlyList<ProductView>> HandleAsync(CancellationToken cancellationToken = default);
}

public interface IGetProductByIdHandler
{
    Task<ProductView?> HandleAsync(int id, CancellationToken cancellationToken = default);
}

public interface ICreateProductHandler
{
    Task<ProductView> HandleAsync(CreateProductCommand command, CancellationToken cancellationToken = default);
}

internal static class ProductMapping
{
    public static ProductView ToView(this Product product) =>
        new(product.Id, product.Name, product.Price.Amount, product.Stock);
}
