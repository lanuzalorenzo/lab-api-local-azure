using EcommerceApi.Domain.Orders;

namespace EcommerceApi.Application.Orders;

/// <summary>
/// Write-side command. It deliberately carries only the data needed to build the
/// aggregate; the identity of the caller comes from the validated token.
/// </summary>
public sealed record CreateOrderCommand(string UserId, IReadOnlyList<int> ProductIds);

/// <summary>
/// Outcome of the write use case. Expected failures are values, not exceptions, so
/// the API can map them to 400 without a try/catch per endpoint.
/// </summary>
public sealed record CreateOrderResult
{
    public Order? Order { get; private init; }

    public IReadOnlyList<int> MissingProductIds { get; private init; } = [];

    public IReadOnlyList<string> Errors { get; private init; } = [];

    public bool Succeeded => Order is not null;

    public static CreateOrderResult Success(Order order) => new() { Order = order };

    public static CreateOrderResult MissingProducts(IReadOnlyList<int> productIds) =>
        new() { MissingProductIds = productIds };

    public static CreateOrderResult Invalid(IReadOnlyList<string> errors) =>
        new() { Errors = errors };
}
