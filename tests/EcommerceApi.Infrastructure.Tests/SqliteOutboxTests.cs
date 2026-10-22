using EcommerceApi.Application.Abstractions;
using EcommerceApi.Application.Orders;
using EcommerceApi.Application.Outbox;
using EcommerceApi.Contracts;
using EcommerceApi.Domain.Common;
using EcommerceApi.Domain.Orders;
using EcommerceApi.Infrastructure.Outbox;
using EcommerceApi.Infrastructure.Persistence;
using EcommerceApi.Infrastructure.Processing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EcommerceApi.Infrastructure.Tests;

public sealed class SqliteOutboxTests : IAsyncLifetime
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"ecommerce-outbox-{Guid.NewGuid():N}.db");

    private SqliteConnectionFactory _factory = null!;
    private SqliteSchemaInitializer _initializer = null!;

    private SqliteSession CreateSession() => new(_factory);

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

    private static Order NewOrder(string userId = "user-1")
    {
        var line = new OrderLine(1, Money.From(99.99m), 1);

        return Order.Create(userId, [line], new DateTime(2026, 10, 22, 10, 0, 0, DateTimeKind.Utc));
    }

    private static OutboxMessage NewMessage(int orderId, string orderPayload) =>
        new($"{orderId}:v1", OrderCreatedV1.Type, orderPayload, new DateTime(2026, 10, 22, 10, 0, 0, DateTimeKind.Utc));

    private static OutboxMessage NewMessageFor(Order order) =>
        new($"{order.Id}:v1", OrderCreatedV1.Type, "{}", DateTime.UtcNow);

    [Fact]
    public async Task Commit_PersistsOrderAndOutboxTogether()
    {
        await using var session = CreateSession();
        var unitOfWork = new SqliteUnitOfWork(session);
        var orders = new SqliteOrderRepository(session);
        var outbox = new SqliteOutboxStore(session);

        await using var transaction = await unitOfWork.BeginTransactionAsync();

        var order = NewOrder();
        await orders.AddAsync(order);
        await outbox.AddAsync(NewMessageFor(order));

        await transaction.CommitAsync();

        var readModel = new SqliteOrderReadModel(session);
        var summary = await readModel.FindAsync(order.Id, "user-1");
        var pending = await outbox.GetPendingAsync(10);

        Assert.NotNull(summary);
        Assert.Single(pending);
        Assert.Equal($"{order.Id}:v1", pending[0].MessageId);
    }

    [Fact]
    public async Task Rollback_LeavesNeitherOrderNorOutboxMessage()
    {
        int orderId;

        await using (var session = CreateSession())
        {
            var unitOfWork = new SqliteUnitOfWork(session);
            var orders = new SqliteOrderRepository(session);
            var outbox = new SqliteOutboxStore(session);

            await using (var transaction = await unitOfWork.BeginTransactionAsync())
            {
                var order = NewOrder("user-rollback");
                await orders.AddAsync(order);
                await outbox.AddAsync(NewMessageFor(order));
                orderId = order.Id;
                // No commit: disposal rolls back.
            }

            var readModel = new SqliteOrderReadModel(session);
            var summary = await readModel.FindAsync(orderId, "user-rollback");
            var pending = await outbox.GetPendingAsync(10);

            Assert.Null(summary);
            Assert.Empty(pending);
        }
    }

    [Fact]
    public async Task DuplicateMessageId_IsRejectedByTheUniqueConstraint()
    {
        await using var session = CreateSession();
        var outbox = new SqliteOutboxStore(session);
        var message = NewMessage(42, "{}");

        await outbox.AddAsync(message);

        await Assert.ThrowsAsync<SqliteException>(() => outbox.AddAsync(message));
    }

    [Fact]
    public async Task Publisher_MarksMessageAsProcessed_WhenPublishingSucceeds()
    {
        await using var session = CreateSession();
        var store = new SqliteOutboxStore(session);
        await store.AddAsync(NewMessage(1, "{}"));

        var publisher = new OutboxPublisher(
            store,
            new RecordingPublisher(),
            TimeProvider.System,
            NullLogger<OutboxPublisher>.Instance);

        var published = await publisher.PublishPendingAsync();

        Assert.Equal(1, published);
        Assert.Empty(await store.GetPendingAsync(10));
    }

    [Fact]
    public async Task Publisher_DeadLettersMessage_AfterMaxAttempts()
    {
        await using var session = CreateSession();
        var store = new SqliteOutboxStore(session);
        await store.AddAsync(NewMessage(2, "{}"));

        var publisher = new OutboxPublisher(
            store,
            new FailingPublisher(),
            TimeProvider.System,
            NullLogger<OutboxPublisher>.Instance);

        for (var attempt = 1; attempt <= OutboxPublisher.MaxAttempts; attempt++)
        {
            await publisher.PublishPendingAsync();
        }

        var pending = await store.GetPendingAsync(10);
        Assert.Empty(pending);
    }

    [Fact]
    public async Task Dispatcher_ProcessesPendingMessages_WithoutDisposalErrors()
    {
        var logger = new RecordingLogger<OutboxDispatcher>();
        var publisher = new RecordingPublisher();

        var services = new ServiceCollection();
        services.AddSingleton(_factory);
        services.AddScoped<SqliteSession>();
        services.AddScoped<SqliteOutboxStore>();
        services.AddScoped<IOutboxStore>(provider =>
            provider.GetRequiredService<SqliteOutboxStore>());
        services.AddScoped<IEventPublisher>(_ => publisher);
        services.AddScoped(provider => new OutboxPublisher(
            provider.GetRequiredService<IOutboxStore>(),
            provider.GetRequiredService<IEventPublisher>(),
            TimeProvider.System,
            NullLogger<OutboxPublisher>.Instance));

        await using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });

        await using (var seed = provider.CreateAsyncScope())
        {
            await seed.ServiceProvider
                .GetRequiredService<SqliteOutboxStore>()
                .AddAsync(NewMessage(3, "{}"));
        }

        var dispatcher = new OutboxDispatcher(
            provider.GetRequiredService<IServiceScopeFactory>(),
            logger);

        await dispatcher.StartAsync(CancellationToken.None);

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            await using var check = provider.CreateAsyncScope();
            var pending = await check.ServiceProvider
                .GetRequiredService<IOutboxStore>()
                .GetPendingAsync(10);

            if (pending.Count == 0)
            {
                break;
            }

            await Task.Delay(200);
        }

        await dispatcher.StopAsync(CancellationToken.None);
        dispatcher.Dispose();

        Assert.Empty(logger.Errors);
    }

    [Fact]
    public async Task ProcessedMessageStore_IsIdempotent()
    {
        await using var session = CreateSession();
        var store = new SqliteProcessedMessageStore(session);

        await store.MarkProcessedAsync("1:v1", DateTime.UtcNow);
        await store.MarkProcessedAsync("1:v1", DateTime.UtcNow);

        Assert.True(await store.ExistsAsync("1:v1"));
    }

    private sealed class RecordingPublisher : IEventPublisher
    {
        public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FailingPublisher : IEventPublisher
    {
        public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated broker failure.");
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<Exception?> Errors { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
            {
                Errors.Add(exception);
            }
        }
    }
}
