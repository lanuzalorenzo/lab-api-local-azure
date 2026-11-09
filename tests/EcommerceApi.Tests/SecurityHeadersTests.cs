using System.Net;
using Xunit;

namespace EcommerceApi.Tests;

/// <summary>
/// The shared middleware writes a restrictive policy: this API only returns
/// JSON and must not load or embed anything.
/// </summary>
public sealed class SecurityHeadersTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SecurityHeadersTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Responses_CarryTheRestrictiveContentSecurityPolicy()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'",
            response.Headers.GetValues("Content-Security-Policy").Single());
    }
}
