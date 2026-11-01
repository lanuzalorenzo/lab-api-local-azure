using System.Text;
using EcommerceApi.Application.Orders;
using EcommerceApi.Domain.Common;
using EcommerceApi.Domain.Orders;
using EcommerceApi.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using Xunit;

namespace EcommerceApi.Infrastructure.Tests;

public sealed class SqliteOrderReadModelTests : IAsyncLifetime
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"ecommerce-read-model-{Guid.NewGuid():N}.db");

    private SqliteConnectionFactory _factory = null!;
    private SqliteSchemaInitializer _initializer = null!;

    public async Task InitializeAsync()
    {
        _factory = new SqliteConnectionFactory(Options.Create(new StorageOptions
        {
            ConnectionString = $"Data Source={_databasePath};Pooling=False"
        }));
        _initializer = new SqliteSchemaInitializer(_factory);
        await _initializer.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var path = _databasePath + suffix;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        return Task.CompletedTask;
    }

    private SqliteSession CreateSession() => new(_factory);

    private async Task<int> InsertOrderAsync(
        string userId,
        DateTime createdAt,
        params int[] productIds)
    {
        await using var session = CreateSession();
        var orders = new SqliteOrderRepository(session);
        var lines = productIds
            .Select(productId => new OrderLine(productId, Money.From(9.99m), 1))
            .ToList();
        var order = Order.Create(userId, lines, createdAt);

        await orders.AddAsync(order);

        return order.Id;
    }

    private static DateTime Utc(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetByUserAsync_WithLimit_ReturnsTheCursorAndTheNextPage()
    {
        var first = await InsertOrderAsync("user", Utc(2026, 1, 10), 1);
        var second = await InsertOrderAsync("user", Utc(2026, 1, 11), 2);
        var third = await InsertOrderAsync("user", Utc(2026, 1, 12), 3);
        await InsertOrderAsync("other", Utc(2026, 1, 13), 4);

        await using var session = CreateSession();
        var readModel = new SqliteOrderReadModel(session);

        var firstPage = await readModel.GetByUserAsync("user", new OrderQuery(Limit: 2));

        Assert.Equal([first, second], firstPage.Items.Select(order => order.Id));
        Assert.Equal([1], firstPage.Items[0].ProductIds);
        Assert.Equal([2], firstPage.Items[1].ProductIds);
        Assert.Equal(second, firstPage.NextCursor);

        var secondPage = await readModel.GetByUserAsync(
            "user",
            new OrderQuery(Limit: 2, AfterId: firstPage.NextCursor));

        Assert.Equal([third], secondPage.Items.Select(order => order.Id));
        Assert.Null(secondPage.NextCursor);
    }

    [Fact]
    public async Task GetByUserAsync_WithDateRange_UsesInclusiveFromAndExclusiveTo()
    {
        var atFrom = await InsertOrderAsync("user", Utc(2026, 1, 15), 1);
        var inside = await InsertOrderAsync("user", Utc(2026, 2, 1), 2);
        await InsertOrderAsync("user", Utc(2026, 3, 1), 3);

        await using var session = CreateSession();
        var readModel = new SqliteOrderReadModel(session);

        var page = await readModel.GetByUserAsync(
            "user",
            new OrderQuery(
                Limit: 10,
                From: new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero),
                To: new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)));

        Assert.Equal([atFrom, inside], page.Items.Select(order => order.Id));
    }

    [Fact]
    public async Task KeysetQuery_UsesTheCompositeOwnerIndex()
    {
        await InsertOrderAsync("user", Utc(2026, 1, 10), 1);
        await InsertOrderAsync("user", Utc(2026, 1, 11), 2);
        await InsertOrderAsync("other", Utc(2026, 1, 12), 3);

        await using var connection = await _factory.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            EXPLAIN QUERY PLAN
            SELECT Id, CreatedAt, TotalCents
            FROM Orders
            WHERE UserId = $userId AND Id > $afterId
            ORDER BY Id
            LIMIT 3;
            """;
        command.Parameters.AddWithValue("$userId", "user");
        command.Parameters.AddWithValue("$afterId", 0);

        var plan = new StringBuilder();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            plan.AppendLine(reader.GetString(3));
        }

        Assert.Contains("IX_Orders_UserId_Id", plan.ToString(), StringComparison.Ordinal);
    }
}
