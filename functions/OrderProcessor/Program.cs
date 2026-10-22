using EcommerceApi.Application.Abstractions;
using EcommerceApi.Application.OrderProcessing;
using EcommerceApi.Infrastructure.Persistence;
using EcommerceApi.Infrastructure.Processing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<SqliteSchemaInitializer>();
        services.AddScoped<SqliteSession>();
        services.AddScoped<IProcessedMessageStore, SqliteProcessedMessageStore>();
        services.AddSingleton<IProcessOrderHandler, ProcessOrderHandler>();

        services.AddOptions<StorageOptions>()
            .Configure(options =>
                options.ConnectionString =
                    Environment.GetEnvironmentVariable("Storage__ConnectionString")
                    ?? "Data Source=data/ecommerce.db");
    })
    .Build();

// The consumer shares the SQLite lab database, so the idempotency table must exist
// before the first message arrives.
await host.Services.GetRequiredService<SqliteSchemaInitializer>().InitializeAsync();

await host.RunAsync();
