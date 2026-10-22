using EcommerceApi.Application.Products;
using EcommerceApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceApi.Controllers;

/// <summary>
/// Catalogue endpoints. Products keep a simple CRUD shape: the read and write
/// models do not diverge enough to justify a full CQRS split (see ADR-0003).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly IGetProductsHandler _getProducts;
    private readonly IGetProductByIdHandler _getProductById;
    private readonly ICreateProductHandler _createProduct;

    public ProductsController(
        IGetProductsHandler getProducts,
        IGetProductByIdHandler getProductById,
        ICreateProductHandler createProduct)
    {
        _getProducts = getProducts;
        _getProductById = getProductById;
        _createProduct = createProduct;
    }

    /// <summary>
    /// Get all products.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ProductView>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProductView>>> GetAllProducts(
        CancellationToken cancellationToken)
    {
        return Ok(await _getProducts.HandleAsync(cancellationToken));
    }

    /// <summary>
    /// Get a product by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProductView), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductView>> GetProductById(
        int id,
        CancellationToken cancellationToken)
    {
        var product = await _getProductById.HandleAsync(id, cancellationToken);
        if (product is null)
        {
            return NotFound(new { message = $"Product with ID {id} not found" });
        }

        return Ok(product);
    }

    /// <summary>
    /// Create a new product.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(16 * 1024)]
    [ProducesResponseType(typeof(ProductView), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductView>> CreateProduct(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _createProduct.HandleAsync(
            new CreateProductCommand(request.Name, request.Price, request.Stock),
            cancellationToken);

        return CreatedAtAction(nameof(GetProductById), new { id = product.Id }, product);
    }
}
