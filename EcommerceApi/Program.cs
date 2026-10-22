using System.Threading.RateLimiting;
using EcommerceApi.Application.Abstractions;
using EcommerceApi.Application.OrderProcessing;
using EcommerceApi.Application.Orders;
using EcommerceApi.Application.Products;
using EcommerceApi.Configuration;
using EcommerceApi.ErrorHandling;
using EcommerceApi.Infrastructure.Outbox;
using EcommerceApi.Infrastructure.Persistence;
using EcommerceApi.Infrastructure.Processing;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var azureAdOptions = builder.Configuration
    .GetSection(AzureAdOptions.SectionName)
    .Get<AzureAdOptions>() ?? new AzureAdOptions();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddHealthChecks();

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

// CORS is deny-by-default. Browser clients must be explicitly allowlisted through
// configuration (Cors__AllowedOrigins__0, ...). Same-origin calls (Swagger) do not
// require CORS.
builder.Services.AddCors(options =>
{
    options.AddPolicy("ApiCors", policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            policy.SetIsOriginAllowed(_ => false);
            return;
        }

        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Basic abuse protection: 120 requests per minute per source IP. This is a lab
// control, not a substitute for an API gateway or WAF.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = azureAdOptions.Authority;
        options.Audience = azureAdOptions.Audience;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = azureAdOptions.Authority,
            ValidateAudience = true,
            ValidAudience = azureAdOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (!azureAdOptions.IsConfigured)
{
    app.Logger.LogWarning(
        "AzureAd is using placeholder values. Configure a real tenant before validating tokens. See docs/azure-ad-config.md.");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();

    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }
}

app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
    context.Response.Headers.TryAdd("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
    await next();
});

app.UseCors("ApiCors");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

await app.Services.GetRequiredService<SqliteSchemaInitializer>().InitializeAsync();

app.Run();

// Exposed so integration tests can use WebApplicationFactory<Program>.
public partial class Program { }
