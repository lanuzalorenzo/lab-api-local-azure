using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EcommerceApi.Application.Products;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EcommerceApi.Tests;

/// <summary>
/// Pins the error contract: every failure that carries a body uses ProblemDetails,
/// includes a trace identifier and never leaks the exception message or a stack
/// trace. The 500 case forces the failure through a throwing handler so the default
/// exception middleware is exercised, not a hand-written branch.
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

    [Fact]
    public async Task MissingProduct_ReturnsProblemDetailsWithDetail()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "probe");

        var response = await client.PostAsJsonAsync("/api/orders", new { productIds = new[] { 999999 } });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Products not found: 999999");
    }

    [Fact]
    public async Task UnknownOrder_ReturnsProblemDetailsWithDetail()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "probe");

        var response = await client.GetAsync("/api/orders/999999");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Order with ID 999999 not found");
    }

    [Fact]
    public async Task InvalidQueryParameter_ReturnsProblemDetailsWithDetail()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "probe");

        var response = await client.GetAsync("/api/orders?limit=0");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "limit must be between 1 and 100.");
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedDetail)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.Equal((int)expectedStatus, root.GetProperty("status").GetInt32());
        Assert.Contains(expectedDetail, root.GetProperty("detail").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
    }

    private sealed class ThrowingProductsHandler : IGetProductsHandler
    {
        public Task<IReadOnlyList<ProductView>> HandleAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(Secret);
    }
}
