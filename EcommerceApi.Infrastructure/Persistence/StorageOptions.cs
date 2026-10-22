namespace EcommerceApi.Infrastructure.Persistence;

/// <summary>
/// Strongly typed storage configuration. The connection string is configuration,
/// not a secret: the lab uses a local SQLite file.
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string ConnectionString { get; set; } = "Data Source=data/ecommerce.db";
}
