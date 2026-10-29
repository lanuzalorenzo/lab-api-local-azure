using EcommerceApi.Application.Orders;
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

        var ownerOrders = await ownerClient.GetFromJsonAsync<OrderPage>("/api/orders");
        var otherOrders = await otherClient.GetFromJsonAsync<OrderPage>("/api/orders");

        Assert.NotNull(ownerOrders);
        Assert.NotNull(otherOrders);
        Assert.Single(ownerOrders.Items);
        Assert.Empty(otherOrders.Items);
    }

    [Fact]
    public async Task GetOrders_WithLimit_ReturnsCursorAndNextPage()
    {
        using var client = CreateClient($"page-owner-{Guid.NewGuid():N}");

        for (var index = 0; index < 3; index++)
        {
            var createResponse = await client.PostAsJsonAsync(
                "/api/orders",
                new { productIds = new[] { 1 } });
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        }

        var first = await client.GetFromJsonAsync<OrderPage>("/api/orders?limit=2");
        Assert.NotNull(first);
        Assert.Equal(2, first.Items.Count);
        Assert.NotNull(first.NextCursor);
        Assert.Equal(first.Items[^1].Id, first.NextCursor);

        var cursor = first.NextCursor!.Value;

        var second = await client.GetFromJsonAsync<OrderPage>(
            $"/api/orders?limit=2&afterId={cursor}");
        Assert.NotNull(second);
        Assert.Single(second.Items);
        Assert.Null(second.NextCursor);
        Assert.All(second.Items, order => Assert.True(order.Id > cursor));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task GetOrders_WithInvalidLimit_ReturnsBadRequest(int limit)
    {
        using var client = CreateClient($"invalid-limit-{Guid.NewGuid():N}");

        var response = await client.GetAsync($"/api/orders?limit={limit}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetOrders_WithNegativeCursor_ReturnsBadRequest()
    {
        using var client = CreateClient($"invalid-cursor-{Guid.NewGuid():N}");

        var response = await client.GetAsync("/api/orders?afterId=-1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetOrders_WithFromNotEarlierThanTo_ReturnsBadRequest()
    {
        using var client = CreateClient($"invalid-range-{Guid.NewGuid():N}");

        var response = await client.GetAsync(
            "/api/orders?from=2026-10-29T12:00:00Z&to=2026-10-29T11:00:00Z");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetOrders_WithValidDateRange_ReturnsOk()
    {
        using var client = CreateClient($"valid-range-{Guid.NewGuid():N}");

        var response = await client.GetAsync(
            "/api/orders?from=2026-10-01T00:00:00Z&to=2026-11-01T00:00:00Z");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
