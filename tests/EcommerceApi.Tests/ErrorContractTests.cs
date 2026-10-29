using System.Net;
using System.Text.Json;
using EcommerceApi.Application.Products;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EcommerceApi.Tests;

/// <summary>
/// Pins the contract for unexpected failures: a 500 must be serialized as
/// ProblemDetails with a trace identifier and must not leak the exception
/// message or a stack trace.
/// </summary>
public sealed class ErrorContractTests
{
    private const string Secret = "boom-secreto";

    [Fact]
    public async Task UnhandledException_ReturnsProblemDetailsWithoutInternalDetails()
    {
        using var factory = new ApiFactory();
        using var client = factory
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.AddScoped<IGetProductsHandler, ThrowingProductsHandler>()))
            .CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "probe");

        var response = await client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Secret, body);
        Assert.DoesNotContain("InvalidOperationException", body);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal(500, root.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
        Assert.False(root.TryGetProperty("detail", out _));
        Assert.False(root.TryGetProperty("exception", out _));
    }

    private sealed class ThrowingProductsHandler : IGetProductsHandler
    {
        public Task<IReadOnlyList<ProductView>> HandleAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(Secret);
    }
}
