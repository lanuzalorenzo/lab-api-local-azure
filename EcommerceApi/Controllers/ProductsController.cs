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
    public ActionResult<IEnumerable<ProductView>> GetAllProducts()
    {
        return Ok(_getProducts.Handle());
    }

    /// <summary>
    /// Get a product by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProductView), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ProductView> GetProductById(int id)
    {
        var product = _getProductById.Handle(id);
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
    public ActionResult<ProductView> CreateProduct([FromBody] CreateProductRequest request)
    {
        var product = _createProduct.Handle(
            new CreateProductCommand(request.Name, request.Price, request.Stock));

        return CreatedAtAction(nameof(GetProductById), new { id = product.Id }, product);
    }
}
