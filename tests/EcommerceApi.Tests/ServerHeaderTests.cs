using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace EcommerceApi.Tests;

/// <summary>
/// Kestrel adds "Server: Kestrel" to every response unless the option is off.
/// The option is set by UseSecurityHeaders (LL.Security); TestServer never writes
/// the header, so the HTTP-level absence is checked against the container in CI.
/// This test fails if the package stops applying it or the app overrides it.
/// </summary>
public sealed class ServerHeaderTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ServerHeaderTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Kestrel_DoesNotAdvertiseTheServerName()
    {
        var options = _factory.Services
            .GetRequiredService<IOptions<KestrelServerOptions>>()
            .Value;

        Assert.False(options.AddServerHeader);
    }
}
