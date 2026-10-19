namespace EcommerceApi.Configuration;

/// <summary>
/// Strongly typed configuration for the Microsoft Entra ID (Azure AD) integration.
/// </summary>
public sealed class AzureAdOptions
{
    public const string SectionName = "AzureAd";

    public string TenantId { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// Returns false when the repository still contains the public placeholders.
    /// The API can start with placeholders, but token validation will fail until
    /// a real tenant is configured.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(TenantId) &&
        !TenantId.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(Audience) &&
        !Audience.Contains("YOUR_", StringComparison.OrdinalIgnoreCase);
}
