using System.Security.Claims;
using EcommerceApi.Application.Orders;
using EcommerceApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceApi.Controllers;

/// <summary>
/// Order endpoints. Reads and writes go through separate handlers (CQRS-lite):
/// commands mutate the aggregate, queries return a flat projection.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly ICreateOrderHandler _createOrder;
    private readonly IGetOrdersHandler _getOrders;
    private readonly IGetOrderByIdHandler _getOrderById;

    public OrdersController(
        ICreateOrderHandler createOrder,
        IGetOrdersHandler getOrders,
        IGetOrderByIdHandler getOrderById)
    {
        _createOrder = createOrder;
        _getOrders = getOrders;
        _getOrderById = getOrderById;
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
    [ProducesResponseType(typeof(IEnumerable<OrderSummary>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<OrderSummary>> GetAllOrders()
    {
        return Ok(_getOrders.Handle(CurrentUserId));
    }

    /// <summary>
    /// Get one order owned by the authenticated user. Orders owned by other
    /// users are deliberately reported as 404 to avoid object enumeration.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(OrderSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<OrderSummary> GetOrderById(int id)
    {
        var order = _getOrderById.Handle(id, CurrentUserId);
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
    [ProducesResponseType(typeof(OrderSummary), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderSummary>> CreateOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _createOrder.HandleAsync(
            new CreateOrderCommand(CurrentUserId, request.ProductIds),
            cancellationToken);

        if (result.MissingProductIds.Count > 0)
        {
            return BadRequest(new
            {
                message = $"Products not found: {string.Join(", ", result.MissingProductIds)}"
            });
        }

        if (!result.Succeeded)
        {
            return BadRequest(new { message = string.Join(" ", result.Errors) });
        }

        var summary = OrderSummary.From(result.Order!);

        return CreatedAtAction(nameof(GetOrderById), new { id = summary.Id }, summary);
    }
}
