using Azure.Messaging.ServiceBus;
using EcommerceApi.Application.Outbox;
using EcommerceApi.Infrastructure.Outbox;

// Publica un mensaje con el adaptador real de Infrastructure y lo consume de la cola.
// Sale 0 si llega con los mismos campos; 1 si no llega, si el emulador no responde
// o si la cadena de conexión o el nombre de la cola no son válidos. Se ejecuta con
// scripts/servicebus-emulator.sh smoke, nunca desde `dotnet test`.
const string DefaultConnectionString =
    "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

const string MessageType = "OrderCreated";
const string ContentType = "application/json";
var wait = TimeSpan.FromSeconds(30);

var connectionString =
    Environment.GetEnvironmentVariable("SERVICEBUS_EMULATOR_CONNECTION_STRING") ?? DefaultConnectionString;
var queueName = Environment.GetEnvironmentVariable("SERVICEBUS_EMULATOR_QUEUE") ?? "orders";

var messageId = Guid.NewGuid().ToString("N");
var payload = $"{{\"orderId\":\"{messageId}\"}}";
Console.WriteLine($"Cola {queueName}; mensaje {messageId}");

var options = new ServiceBusOptions { ConnectionString = connectionString, QueueName = queueName };

try
{
    await using (var publisher = new ServiceBusEventPublisher(options))
    {
        await publisher.PublishAsync(new OutboxMessage(messageId, MessageType, payload, DateTime.UtcNow));
    }

    Console.WriteLine("Publicado por ServiceBusEventPublisher");

    await using var client = new ServiceBusClient(connectionString);
    await using var receiver = client.CreateReceiver(queueName, new ServiceBusReceiverOptions
    {
        ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
    });

    // El plazo del SDK devuelve nulo; el token es el tope duro si el SDK no responde.
    using var timeout = new CancellationTokenSource(wait + TimeSpan.FromSeconds(5));
    var received = await receiver.ReceiveMessageAsync(wait, timeout.Token);

    if (received is null)
    {
        Console.Error.WriteLine($"No llegó ningún mensaje en {wait.TotalSeconds:0} s");
        return 1;
    }

    var body = received.Body.ToString();
    var mismatches = new List<string>();

    if (received.MessageId != messageId)
    {
        mismatches.Add($"MessageId: esperado {messageId}, recibido {received.MessageId}");
    }

    if (received.Subject != MessageType)
    {
        mismatches.Add($"Subject: esperado {MessageType}, recibido {received.Subject}");
    }

    if (received.ContentType != ContentType)
    {
        mismatches.Add($"ContentType: esperado {ContentType}, recibido {received.ContentType}");
    }

    if (body != payload)
    {
        mismatches.Add($"Cuerpo: esperado {payload}, recibido {body}");
    }

    if (mismatches.Count > 0)
    {
        foreach (var mismatch in mismatches)
        {
            Console.Error.WriteLine(mismatch);
        }

        return 1;
    }

    Console.WriteLine(
        $"Recibido y verificado: id={received.MessageId} subject={received.Subject} " +
        $"contentType={received.ContentType} cuerpo={body}");

    return 0;
}
catch (ServiceBusException exception)
{
    Console.Error.WriteLine($"El emulador no respondió: {exception.Reason}. {exception.Message}");
    return 1;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine($"No llegó respuesta del emulador en {wait.TotalSeconds:0} s");
    return 1;
}
catch (Exception exception) when (exception is ArgumentException or FormatException)
{
    Console.Error.WriteLine($"La configuración no es válida: {exception.Message}");
    return 1;
}
