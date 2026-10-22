using EcommerceApi.Application.Abstractions;
using EcommerceApi.Domain.Products;

namespace EcommerceApi.Application.Products;

public sealed class GetProductsHandler : IGetProductsHandler
{
    private readonly IProductRepository _products;

    public GetProductsHandler(IProductRepository products) => _products = products;

    public IReadOnlyList<ProductView> Handle() =>
        _products.GetAll().Select(product => product.ToView()).ToList();
}

public sealed class GetProductByIdHandler : IGetProductByIdHandler
{
    private readonly IProductRepository _products;

    public GetProductByIdHandler(IProductRepository products) => _products = products;

    public ProductView? Handle(int id) => _products.Find(id)?.ToView();
}

public sealed class CreateProductHandler : ICreateProductHandler
{
    private readonly IProductRepository _products;

    public CreateProductHandler(IProductRepository products) => _products = products;

    public ProductView Handle(CreateProductCommand command)
    {
        var product = Product.Create(command.Name, command.Price, command.Stock);
        _products.Add(product);

        return product.ToView();
    }
}
