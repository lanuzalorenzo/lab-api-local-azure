using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace EcommerceApi.Emulator.Tests;

/// <summary>
/// Arranca la API con su composición real y solo cambia la configuración. La
/// cadena del emulador es la pública de Microsoft: no lleva credenciales.
/// </summary>
public sealed class EmulatorApiFactory : WebApplicationFactory<Program>
{
    public const string DefaultConnectionString =
        "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    private const string PublisherModeVariable = "Publisher__Mode";
    private const string ConnectionStringVariable = "ServiceBus__ConnectionString";
    private const string QueueNameVariable = "ServiceBus__QueueName";

    private readonly string? _previousPublisherMode;
    private readonly string? _previousConnectionString;
    private readonly string? _previousQueueName;
    private readonly string _queueName;
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"ecommerce-api-emulator-{Guid.NewGuid():N}.db");

    public EmulatorApiFactory(string publisherMode = "ServiceBus", string? queueName = null)
    {
        _queueName = queueName ?? QueueName;

        // El host resuelve `Publisher:Mode` y `ServiceBus` al registrar servicios,
        // antes de que se aplique la configuración de la fábrica. Las variables de
        // entorno sí están cuando `Program.cs` decide, y son la misma vía que
        // documenta el runbook. Se guardan y se restauran al liberar la fábrica.
        _previousPublisherMode = Environment.GetEnvironmentVariable(PublisherModeVariable);
        _previousConnectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        _previousQueueName = Environment.GetEnvironmentVariable(QueueNameVariable);
        Environment.SetEnvironmentVariable(PublisherModeVariable, publisherMode);
        Environment.SetEnvironmentVariable(ConnectionStringVariable, ConnectionString);
        Environment.SetEnvironmentVariable(QueueNameVariable, _queueName);
    }

    public static string ConnectionString { get; } =
        Environment.GetEnvironmentVariable("SERVICEBUS_EMULATOR_CONNECTION_STRING")
        ?? DefaultConnectionString;

    public static string QueueName { get; } =
        Environment.GetEnvironmentVariable("SERVICEBUS_EMULATOR_QUEUE") ?? "orders";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAd:TenantId"] = "00000000-0000-0000-0000-000000000000",
                ["AzureAd:ClientId"] = "11111111-1111-1111-1111-111111111111",
                ["AzureAd:Audience"] = "api://11111111-1111-1111-1111-111111111111",
                ["AzureAd:Authority"] = "https://login.microsoftonline.com/00000000-0000-0000-0000-000000000000/v2.0",
                ["Cors:AllowedOrigins:0"] = "https://frontend.test",
                ["Storage:ConnectionString"] = $"Data Source={_databasePath};Pooling=False"
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        Environment.SetEnvironmentVariable(PublisherModeVariable, _previousPublisherMode);
        Environment.SetEnvironmentVariable(ConnectionStringVariable, _previousConnectionString);
        Environment.SetEnvironmentVariable(QueueNameVariable, _previousQueueName);

        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var path = _databasePath + suffix;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
