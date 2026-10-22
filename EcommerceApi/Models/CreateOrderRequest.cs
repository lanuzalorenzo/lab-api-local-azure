using System.ComponentModel.DataAnnotations;

namespace EcommerceApi.Models;

/// <summary>
/// Input DTO for order creation. The client sends product identifiers only: prices,
/// totals and ownership are resolved server-side.
/// </summary>
public sealed class CreateOrderRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(20)]
    public List<int> ProductIds { get; set; } = new List<int>();
}
