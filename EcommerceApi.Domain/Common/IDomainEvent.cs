namespace EcommerceApi.Domain.Common;

/// <summary>
/// Marker for a fact that happened inside the domain. Domain events are raised by
/// aggregates and translated into integration events by the application layer;
/// they are never published directly to a broker.
/// </summary>
public interface IDomainEvent
{
}
