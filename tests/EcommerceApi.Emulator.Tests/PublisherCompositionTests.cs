using System.Net.Sockets;
using Azure.Messaging.ServiceBus;
using EcommerceApi.Application.Abstractions;
using EcommerceApi.Application.Outbox;
using EcommerceApi.Infrastructure.Outbox;
using Microsoft.Extensions.DependencyInjection;

namespace EcommerceApi.Emulator.Tests;

/// <summary>
/// Prueba la composición del camino de mensajería, no el adaptador aislado: el
/// host resuelve <see cref="IEventPublisher"/> desde la configuración y publica en
/// la cola real del emulador. El emulador lo arranca el workflow o, en local,
/// <c>scripts/servicebus-emulator.sh up</c>.
/// </summary>
public sealed class PublisherCompositionTests
{
    [Fact]
    public void ServiceBusMode_RegistersTheServiceBusPublisher()
    {
        using var factory = new EmulatorApiFactory("ServiceBus");

        var publisher = factory.Services.GetRequiredService<IEventPublisher>();

        Assert.IsType<ServiceBusEventPublisher>(publisher);
    }

    [Fact]
    public void ServiceBusMode_BindsTheEmulatorOptionsFromConfiguration()
    {
        // Una cola distinta de la predeterminada (`orders`): si `Program.cs`
        // ignorase la clave, la opción seguiría con su valor por defecto.
        using var factory = new EmulatorApiFactory("ServiceBus", "orders-binding");

        var options = factory.Services.GetRequiredService<ServiceBusOptions>();

        Assert.Equal(EmulatorApiFactory.ConnectionString, options.ConnectionString);
        Assert.Equal("orders-binding", options.QueueName);
    }

    [Fact]
    public void LogMode_DoesNotRegisterTheServiceBusPublisher()
    {
        using var factory = new EmulatorApiFactory("Log");

        var publisher = factory.Services.GetRequiredService<IEventPublisher>();

        Assert.IsType<LoggingEventPublisher>(publisher);
    }

    [Fact]
    public async Task ServiceBusMode_PublishesThroughTheComposition()
    {
        await EnsureEmulatorIsReachableAsync();

        using var factory = new EmulatorApiFactory("ServiceBus");
        var publisher = factory.Services.GetRequiredService<IEventPublisher>();

        var messageId = Guid.NewGuid().ToString("N");
        var payload = $"{{\"orderId\":\"{messageId}\"}}";
        await publisher.PublishAsync(
            new OutboxMessage(messageId, "OrderCreated", payload, DateTime.UtcNow));

        var received = await ReceiveAsync(messageId);

        Assert.NotNull(received);
        Assert.Equal(messageId, received.MessageId);
        Assert.Equal("OrderCreated", received.Subject);
        Assert.Equal("application/json", received.ContentType);
        Assert.Equal(payload, received.Body.ToString());
    }

    private static async Task<ServiceBusReceivedMessage?> ReceiveAsync(string messageId)
    {
        await using var client = new ServiceBusClient(EmulatorApiFactory.ConnectionString);
        await using var receiver = client.CreateReceiver(
            EmulatorApiFactory.QueueName,
            new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.PeekLock });

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var message = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(5));
            if (message is null)
            {
                continue;
            }

            if (message.MessageId == messageId)
            {
                await receiver.CompleteMessageAsync(message);
                return message;
            }

            // Un mensaje ajeno no se pierde: se devuelve a la cola.
            await receiver.AbandonMessageAsync(message);
        }

        return null;
    }

    private static async Task EnsureEmulatorIsReachableAsync()
    {
        var properties = ServiceBusConnectionStringProperties.Parse(EmulatorApiFactory.ConnectionString);
        var port = properties.Endpoint.Port > 0 ? properties.Endpoint.Port : 5672;

        using var client = new TcpClient();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await client.ConnectAsync(properties.Endpoint.Host, port, timeout.Token);
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException)
        {
            Assert.Fail(
                $"El emulador no responde en {properties.Endpoint.Host}:{port}. " +
                "Arráncalo con `scripts/servicebus-emulator.sh up` y repite la prueba.");
        }
    }
}
