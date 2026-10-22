using EcommerceApi.Domain.Products;

namespace EcommerceApi.Application.Products;

public sealed record CreateProductCommand(string Name, decimal Price, int Stock);

public interface IGetProductsHandler
{
    IReadOnlyList<ProductView> Handle();
}

public interface IGetProductByIdHandler
{
    ProductView? Handle(int id);
}

public interface ICreateProductHandler
{
    ProductView Handle(CreateProductCommand command);
}

internal static class ProductMapping
{
    public static ProductView ToView(this Product product) =>
        new(product.Id, product.Name, product.Price.Amount, product.Stock);
}
