using Xunit;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace EcommerceApi.Tests;

public sealed class ProductsEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ProductsEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient(string? user = null)
    {
        var client = _factory.CreateClient();
        if (user is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, user);
        }

        return client;
    }

    [Fact]
    public async Task GetProducts_WithoutToken_ReturnsUnauthorized()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProducts_WithAuthenticatedUser_ReturnsOk()
    {
        using var client = CreateClient("products-reader");

        var response = await client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_WithNegativePrice_ReturnsBadRequest()
    {
        using var client = CreateClient("products-writer");

        var response = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Invalid",
            price = -1,
            stock = 1
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_IgnoresClientSuppliedId()
    {
        using var client = CreateClient("products-writer");

        var response = await client.PostAsJsonAsync("/api/products", new
        {
            id = 999_999,
            name = "Mass assignment attempt",
            price = 10,
            stock = 1
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = payload.RootElement.GetProperty("id").GetInt32();

        Assert.NotEqual(999_999, id);
    }

    [Fact]
    public async Task CreateProduct_WithEmptyName_ReturnsBadRequest()
    {
        using var client = CreateClient("products-writer");

        var response = await client.PostAsJsonAsync("/api/products", new
        {
            name = " ",
            price = 10,
            stock = 1
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
