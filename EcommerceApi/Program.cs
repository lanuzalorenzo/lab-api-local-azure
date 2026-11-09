using EcommerceApi.Application.Abstractions;
using EcommerceApi.Application.OrderProcessing;
using EcommerceApi.Application.Orders;
using EcommerceApi.Application.Products;
using EcommerceApi.Configuration;
using EcommerceApi.ErrorHandling;
using EcommerceApi.Infrastructure.Outbox;
using EcommerceApi.Infrastructure.Persistence;
using EcommerceApi.Infrastructure.Processing;
using LL.Hosting;
using LL.Security;

var builder = WebApplication.CreateBuilder(args);

// Include the W3C trace and span identifiers in every log scope. The simple
// console formatter prints them when Logging:Console:FormatterOptions:IncludeScopes
// is enabled, so the traceId returned in a 500 can be matched with the server log.
builder.Logging.Configure(options =>
{
    options.ActivityTrackingOptions = ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId;
});

var azureAdOptions = builder.Configuration
    .GetSection(AzureAdOptions.SectionName)
    .Get<AzureAdOptions>() ?? new AzureAdOptions();

// Kestrel adds "Server: Kestrel" to every response by default, which tells an
// anonymous client which web server is behind the API. The name is not needed
// to serve traffic, and scripts/check-server-header.sh guards its absence.
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddApiProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

// Telemetry is opt-in: without a connection string the SDK is not registered,
// because the Azure Monitor exporter throws on startup when it is missing. The
// deployment sets APPLICATIONINSIGHTS_CONNECTION_STRING in infra/main.bicep.
var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    builder.Services.AddApplicationInsightsTelemetry();
}
builder.Services.AddApiHealthChecks();

// Composition root: the host is the only place that knows about concrete adapters.
// Application and domain code depend only on the ports.
builder.Services.Configure<StorageOptions>(
    builder.Configuration.GetSection(StorageOptions.SectionName));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<SqliteConnectionFactory>();
builder.Services.AddSingleton<SqliteSchemaInitializer>();
builder.Services.AddScoped<SqliteSession>();
builder.Services.AddScoped<IUnitOfWork, SqliteUnitOfWork>();

builder.Services.AddScoped<IProductRepository, SqliteProductRepository>();
builder.Services.AddScoped<IOrderRepository, SqliteOrderRepository>();
builder.Services.AddScoped<IOrderReadModel, SqliteOrderReadModel>();
builder.Services.AddScoped<IProcessedMessageStore, SqliteProcessedMessageStore>();

builder.Services.AddScoped<SqliteOutboxStore>();
builder.Services.AddScoped<IOutboxWriter>(provider => provider.GetRequiredService<SqliteOutboxStore>());
builder.Services.AddScoped<IOutboxStore>(provider => provider.GetRequiredService<SqliteOutboxStore>());
builder.Services.AddScoped<OutboxPublisher>();
builder.Services.AddHostedService<OutboxDispatcher>();

// The local sink proves the outbox pipeline; Service Bus is opt-in and requires a
// real namespace (Publisher__Mode=ServiceBus plus ServiceBus__ConnectionString).
var publisherMode = builder.Configuration["Publisher:Mode"] ?? "Log";
if (string.Equals(publisherMode, "ServiceBus", StringComparison.OrdinalIgnoreCase))
{
    var serviceBusOptions = builder.Configuration
        .GetSection(ServiceBusOptions.SectionName)
        .Get<ServiceBusOptions>() ?? new ServiceBusOptions();
    builder.Services.AddSingleton(serviceBusOptions);
    builder.Services.AddSingleton<IEventPublisher, ServiceBusEventPublisher>();
}
else
{
    builder.Services.AddSingleton<IEventPublisher, LoggingEventPublisher>();
}

builder.Services.AddScoped<IGetProductsHandler, GetProductsHandler>();
builder.Services.AddScoped<IGetProductByIdHandler, GetProductByIdHandler>();
builder.Services.AddScoped<ICreateProductHandler, CreateProductHandler>();

builder.Services.AddScoped<ICreateOrderHandler, CreateOrderHandler>();
builder.Services.AddScoped<IGetOrdersHandler, GetOrdersHandler>();
builder.Services.AddScoped<IGetOrderByIdHandler, GetOrderByIdHandler>();
builder.Services.AddSingleton<IProcessOrderHandler, ProcessOrderHandler>();

// Shared controls live in the LL packages: deny-by-default CORS, hardened JWT,
// rate limiting, security headers, ProblemDetails, HTTPS/HSTS and health checks.
builder.Services.AddDenyByDefaultCors(builder.Configuration);
builder.Services.AddRemoteIpRateLimiting();
builder.Services.AddHardenedJwtBearer(options =>
{
    options.Authority = azureAdOptions.Authority;
    options.Audience = azureAdOptions.Audience;
});
builder.Services.AddAuthorization();

var app = builder.Build();

if (!azureAdOptions.IsConfigured)
{
    app.Logger.LogWarning(
        "AzureAd is using placeholder values. Configure a real tenant before validating tokens. See docs/entra-id.md.");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Headers first: the exception handler clears them when it writes a response, and
// the middleware re-applies them before the response starts. The policy denies
// everything: this API returns JSON and loads nothing.
var securityHeaders = new SecurityHeadersOptions
{
    Csp = ContentSecurityPolicy.DenyAll(),
};
app.UseSecurityHeaders(securityHeaders);
app.UseApiProblemDetails();
app.UseApiTransportSecurity(app.Environment);
app.UseCors("ApiCors");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapApiHealthChecks();

await app.Services.GetRequiredService<SqliteSchemaInitializer>().InitializeAsync();

app.Run();

// Exposed so integration tests can use WebApplicationFactory<Program>.
public partial class Program { }
