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
    /// Get the orders owned by the authenticated user, ordered by identifier
    /// ascending. The response includes the cursor for the next page.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(OrderPage), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderPage>> GetAllOrders(
        [FromQuery] int? limit,
        [FromQuery] int? afterId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var pageSize = limit ?? OrderQuery.DefaultLimit;

        if (pageSize is < 1 or > OrderQuery.MaxLimit)
        {
            return Problem(
                detail: $"limit must be between 1 and {OrderQuery.MaxLimit}.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid request");
        }

        if (afterId is < 0)
        {
            return Problem(
                detail: "afterId must be zero or a positive order identifier.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid request");
        }

        if (from is not null && to is not null && from >= to)
        {
            return Problem(
                detail: "from must be earlier than to.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid request");
        }

        var query = new OrderQuery(pageSize, afterId, from, to);

        return Ok(await _getOrders.HandleAsync(CurrentUserId, query, cancellationToken));
    }

    /// <summary>
    /// Get one order owned by the authenticated user. Orders owned by other
    /// users are deliberately reported as 404 to avoid object enumeration.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(OrderSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderSummary>> GetOrderById(
        int id,
        CancellationToken cancellationToken)
    {
        var order = await _getOrderById.HandleAsync(id, CurrentUserId, cancellationToken);
        if (order is null)
        {
            return Problem(
                detail: $"Order with ID {id} not found",
                statusCode: StatusCodes.Status404NotFound,
                title: "Not found");
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
            return Problem(
                detail: $"Products not found: {string.Join(", ", result.MissingProductIds)}",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid request");
        }

        if (!result.Succeeded)
        {
            return Problem(
                detail: string.Join(" ", result.Errors),
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid request");
        }

        var summary = OrderSummary.From(result.Order!);

        return CreatedAtAction(nameof(GetOrderById), new { id = summary.Id }, summary);
    }
}
