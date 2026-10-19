using System.Text.Json.Serialization;

namespace EcommerceApi.Models;

public class Order
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public List<int> ProductIds { get; set; } = new List<int>();

    public decimal Total { get; set; }

    /// <summary>
    /// Stable identity of the order owner. It is never serialized to clients to
    /// avoid leaking internal identifiers; authorization is enforced server-side.
    /// </summary>
    [JsonIgnore]
    public string UserId { get; set; } = string.Empty;
}
