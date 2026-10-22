using System.Text.Json;
using EcommerceApi.Application.OrderProcessing;
using EcommerceApi.Contracts;
using EcommerceApi.UnitTests.Fakes;
using Xunit;

namespace EcommerceApi.UnitTests.Application;

public sealed class ProcessOrderHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);

    private static string Serialize(OrderCreatedV1 message) =>
        JsonSerializer.Serialize(message, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static OrderCreatedV1 ValidMessage(string messageId = "1:v1") =>
        new(
            messageId,
            1,
            "user-1",
            new DateTime(2026, 10, 22, 10, 0, 0, DateTimeKind.Utc),
            [new OrderCreatedLineV1(1, 100m, 1), new OrderCreatedLineV1(2, 10m, 2)],
            120m);

    private static (ProcessOrderHandler Handler, FakeProcessedMessageStore Store) CreateSut()
    {
        var store = new FakeProcessedMessageStore();

        return (new ProcessOrderHandler(store, new FixedTimeProvider(Now)), store);
    }

    [Fact]
    public async Task HandleAsync_WithValidMessage_MarksItAsProcessed()
    {
        var (handler, store) = CreateSut();

        var result = await handler.HandleAsync("1:v1", Serialize(ValidMessage()));

        Assert.True(result.Processed);
        Assert.False(result.Duplicate);
        Assert.Equal(1, store.MarkProcessedCalls);
    }

    [Fact]
    public async Task HandleAsync_WithDuplicatedMessage_DoesNotRepeatEffects()
    {
        var (handler, store) = CreateSut();
        var payload = Serialize(ValidMessage());

        var first = await handler.HandleAsync("1:v1", payload);
        var second = await handler.HandleAsync("1:v1", payload);

        Assert.True(first.Processed);
        Assert.True(second.Duplicate);
        Assert.Equal(1, store.MarkProcessedCalls);
    }

    [Fact]
    public async Task HandleAsync_WithoutBrokerMessageId_Rejects()
    {
        var (handler, _) = CreateSut();

        var result = await handler.HandleAsync(" ", Serialize(ValidMessage()));

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidJson_Rejects()
    {
        var (handler, _) = CreateSut();

        var result = await handler.HandleAsync("1:v1", "{not-json");

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task HandleAsync_WithMismatchedMessageId_Rejects()
    {
        var (handler, _) = CreateSut();

        var result = await handler.HandleAsync("other-id", Serialize(ValidMessage()));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task HandleAsync_WithNonPositiveQuantity_Rejects()
    {
        var (handler, _) = CreateSut();
        var message = ValidMessage() with
        {
            Items = [new OrderCreatedLineV1(1, 100m, 0)],
            Total = 0m
        };

        var result = await handler.HandleAsync("1:v1", Serialize(message));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("quantity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task HandleAsync_WithTotalMismatch_Rejects()
    {
        var (handler, _) = CreateSut();
        var message = ValidMessage() with { Total = 1m };

        var result = await handler.HandleAsync("1:v1", Serialize(message));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("total", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task HandleAsync_WithRejectedMessage_DoesNotMarkItAsProcessed()
    {
        var (handler, store) = CreateSut();

        await handler.HandleAsync("1:v1", "{not-json");

        Assert.Equal(0, store.MarkProcessedCalls);
    }
}
