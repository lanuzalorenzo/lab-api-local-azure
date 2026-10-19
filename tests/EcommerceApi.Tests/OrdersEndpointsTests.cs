using Xunit;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace EcommerceApi.Tests;

public sealed class OrdersEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public OrdersEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient(string user)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, user);
        return client;
    }

    [Fact]
    public async Task CreateOrder_WithEmptyProductList_ReturnsBadRequest()
    {
        using var client = CreateClient($"empty-{Guid.NewGuid():N}");

        var response = await client.PostAsJsonAsync("/api/orders", new { productIds = Array.Empty<int>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_WithUnknownProduct_ReturnsBadRequest()
    {
        using var client = CreateClient($"unknown-{Guid.NewGuid():N}");

        var response = await client.PostAsJsonAsync("/api/orders", new { productIds = new[] { 999_999 } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserCannotReadAnotherUsersOrder()
    {
        using var ownerClient = CreateClient($"owner-{Guid.NewGuid():N}");
        using var otherClient = CreateClient($"other-{Guid.NewGuid():N}");

        var createResponse = await ownerClient.PostAsJsonAsync("/api/orders", new { productIds = new[] { 1, 2 } });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var payload = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var orderId = payload.RootElement.GetProperty("id").GetInt32();

        var ownerRead = await ownerClient.GetAsync($"/api/orders/{orderId}");
        var otherRead = await otherClient.GetAsync($"/api/orders/{orderId}");

        Assert.Equal(HttpStatusCode.OK, ownerRead.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherRead.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_ReturnsOnlyOwnOrders()
    {
        var owner = $"list-owner-{Guid.NewGuid():N}";
        using var ownerClient = CreateClient(owner);
        using var otherClient = CreateClient($"list-other-{Guid.NewGuid():N}");

        var createResponse = await ownerClient.PostAsJsonAsync("/api/orders", new { productIds = new[] { 1 } });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var ownerOrders = await ownerClient.GetFromJsonAsync<List<JsonElement>>("/api/orders");
        var otherOrders = await otherClient.GetFromJsonAsync<List<JsonElement>>("/api/orders");

        Assert.NotNull(ownerOrders);
        Assert.NotNull(otherOrders);
        Assert.Single(ownerOrders);
        Assert.Empty(otherOrders);
    }
}
