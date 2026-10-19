using System.ComponentModel.DataAnnotations;

namespace EcommerceApi.Models;

public class Product
{
    public int Id { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 1_000_000)]
    public decimal Price { get; set; }

    [Range(0, 1_000_000)]
    public int Stock { get; set; }
}
