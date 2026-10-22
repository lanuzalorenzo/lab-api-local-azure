using EcommerceApi.Application.Abstractions;
using EcommerceApi.Domain.Products;

namespace EcommerceApi.Application.Products;

public sealed class GetProductsHandler : IGetProductsHandler
{
    private readonly IProductRepository _products;

    public GetProductsHandler(IProductRepository products) => _products = products;

    public async Task<IReadOnlyList<ProductView>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var products = await _products.GetAllAsync(cancellationToken);

        return products.Select(product => product.ToView()).ToList();
    }
}

public sealed class GetProductByIdHandler : IGetProductByIdHandler
{
    private readonly IProductRepository _products;

    public GetProductByIdHandler(IProductRepository products) => _products = products;

    public async Task<ProductView?> HandleAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _products.FindAsync(id, cancellationToken);

        return product?.ToView();
    }
}

public sealed class CreateProductHandler : ICreateProductHandler
{
    private readonly IProductRepository _products;

    public CreateProductHandler(IProductRepository products) => _products = products;

    public async Task<ProductView> HandleAsync(
        CreateProductCommand command,
        CancellationToken cancellationToken = default)
    {
        var product = Product.Create(command.Name, command.Price, command.Stock);
        await _products.AddAsync(product, cancellationToken);

        return product.ToView();
    }
}
