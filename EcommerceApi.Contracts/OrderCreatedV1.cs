namespace EcommerceApi.Contracts;

/// <summary>
/// One line of the <c>OrderCreatedV1</c> integration event. The price is the one
/// captured when the order was placed, not the current catalogue price.
/// </summary>
public sealed record OrderCreatedLineV1(int ProductId, decimal UnitPrice, int Quantity);

/// <summary>
/// Integration contract published when an order is placed. It is versioned in the
/// name and in <see cref="MessageId"/> so that consumers can deduplicate safely.
/// Breaking changes must ship as <c>OrderCreatedV2</c>; v1 is never mutated.
/// </summary>
public sealed record OrderCreatedV1(
    string MessageId,
    int OrderId,
    string UserId,
    DateTime CreatedAt,
    IReadOnlyList<OrderCreatedLineV1> Items,
    decimal Total)
{
    public const string Type = "order.created.v1";

    public const int SchemaVersion = 1;

    public static string BuildMessageId(int orderId) => $"{orderId}:v1";
}
