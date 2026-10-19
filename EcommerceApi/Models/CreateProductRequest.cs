using System.ComponentModel.DataAnnotations;

namespace EcommerceApi.Models;

/// <summary>
/// Input DTO for product creation. Keeping a dedicated DTO prevents mass
/// assignment of server-managed fields such as <see cref="Product.Id"/>.
/// </summary>
public sealed class CreateProductRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 1_000_000)]
    public decimal Price { get; set; }

    [Range(0, 1_000_000)]
    public int Stock { get; set; }
}
