using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using EcommerceApi.Models;
using EcommerceApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly InMemoryStore _store;

    public OrdersController(InMemoryStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Stable identity of the caller. Entra ID access tokens expose the object
    /// identifier as oid/sub; ASP.NET Core maps sub to NameIdentifier by default.
    /// </summary>
    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("oid")
        ?? User.FindFirstValue("sub")
        ?? throw new InvalidOperationException("The authenticated principal has no stable user identifier.");

    /// <summary>
    /// Get the orders owned by the authenticated user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Order>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<Order>> GetAllOrders()
    {
        return Ok(_store.GetOrders(CurrentUserId));
    }

    /// <summary>
    /// Get one order owned by the authenticated user. Orders owned by other
    /// users are deliberately reported as 404 to avoid object enumeration.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Order), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Order> GetOrderById(int id)
    {
        var order = _store.GetOrder(id, CurrentUserId);
        if (order is null)
        {
            return NotFound(new { message = $"Order with ID {id} not found" });
        }

        return Ok(order);
    }

    /// <summary>
    /// Create an order for the authenticated user.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(16 * 1024)]
    [ProducesResponseType(typeof(Order), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<Order> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var productIds = request.ProductIds.Distinct().ToList();

        if (!_store.AllProductsExist(productIds))
        {
            var invalidProductIds = productIds
                .Where(productId => _store.GetProduct(productId) is null)
                .ToList();

            return BadRequest(new { message = $"Products not found: {string.Join(", ", invalidProductIds)}" });
        }

        var order = new Order
        {
            CreatedAt = DateTime.UtcNow,
            ProductIds = productIds,
            Total = _store.CalculateTotal(productIds),
            UserId = CurrentUserId
        };

        _store.AddOrder(order);

        return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, order);
    }
}

public sealed class CreateOrderRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(20)]
    public List<int> ProductIds { get; set; } = new List<int>();
}
