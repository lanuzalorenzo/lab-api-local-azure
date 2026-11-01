using System.Net;
using Xunit;

namespace EcommerceApi.Tests;

/// <summary>
/// The test host allowlists https://frontend.test. CORS is deny-by-default: an
/// unknown origin must not receive Access-Control-Allow-Origin.
/// </summary>
public sealed class CorsTests
{
    [Fact]
    public async Task AllowedOrigin_ReceivesCorsHeader()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "https://frontend.test");

        var response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values));
        Assert.Contains("https://frontend.test", values);
    }

    [Fact]
    public async Task UnknownOrigin_DoesNotReceiveCorsHeader()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "https://evil.test");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
