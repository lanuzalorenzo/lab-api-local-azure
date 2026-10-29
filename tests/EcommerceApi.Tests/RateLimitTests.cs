using System.Net;
using Xunit;

namespace EcommerceApi.Tests;

/// <summary>
/// The global limiter allows 120 requests per minute and source IP. The test uses
/// the anonymous health endpoint so no session or database state is involved.
/// </summary>
public sealed class RateLimitTests
{
    [Fact]
    public async Task ExceedingGlobalLimit_ReturnsTooManyRequests()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health");
        for (var i = 1; i <= 120; i++)
        {
            response = await client.GetAsync("/health");
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }
}
