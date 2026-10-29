using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcommerceApi.Application.Products;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace EcommerceApi.Tests;

/// <summary>
/// Observability has three parts: the response carries a W3C trace identifier, the
/// exception is logged at Error level, and the Application Insights SDK is wired to
/// the deployment connection string. The test verifies all three without an Azure
/// resource; the export itself can only be validated after a deployment.
/// </summary>
public sealed class ObservabilityTests
{
    private const string ConnectionString =
        "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=http://127.0.0.1:1/";

    private static readonly Regex W3cTraceId = new(
        "^00-[0-9a-f]{32}-[0-9a-f]{16}-0[01]$",
        RegexOptions.CultureInvariant);

    [Fact]
    public async Task UnhandledException_IsTracedLoggedAndConfiguredForApplicationInsights()
    {
        var recorder = new RecordingLoggerProvider();
        using var factory = new ApiFactory();
        using var host = factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("APPLICATIONINSIGHTS_CONNECTION_STRING", ConnectionString);

                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<ILoggerProvider>(recorder);
                    services.AddScoped<IGetProductsHandler, ThrowingProductsHandler>();
                });
            });
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "probe");

        var response = await client.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var traceId = document.RootElement.GetProperty("traceId").GetString();
        Assert.Matches(W3cTraceId, traceId);

        var record = Assert.Single(recorder.Records, entry =>
            entry.Level == LogLevel.Error &&
            entry.Exception is InvalidOperationException);
        Assert.Contains("unhandled exception", record.Message, StringComparison.OrdinalIgnoreCase);

        var options = host.Services
            .GetRequiredService<IOptions<ApplicationInsightsServiceOptions>>()
            .Value;
        Assert.Equal(ConnectionString, options.ConnectionString);
    }

    private sealed record LogRecord(
        string Category,
        LogLevel Level,
        string Message,
        Exception? Exception,
        IReadOnlyList<object?> Scopes);

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        private readonly AsyncLocal<Stack<object?>?> _scopes = new();

        public ConcurrentQueue<LogRecord> Records { get; } = new();

        public ILogger CreateLogger(string categoryName) =>
            new RecordingLogger(categoryName, Records, _scopes);

        public void Dispose()
        {
        }
    }

    private sealed class RecordingLogger : ILogger
    {
        private readonly string _category;
        private readonly ConcurrentQueue<LogRecord> _records;
        private readonly AsyncLocal<Stack<object?>?> _scopes;

        public RecordingLogger(
            string category,
            ConcurrentQueue<LogRecord> records,
            AsyncLocal<Stack<object?>?> scopes)
        {
            _category = category;
            _records = records;
            _scopes = scopes;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            var stack = _scopes.Value ??= new Stack<object?>();
            stack.Push(state);
            return new Scope(stack);
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _records.Enqueue(new LogRecord(
                _category,
                logLevel,
                formatter(state, exception),
                exception,
                _scopes.Value?.ToArray() ?? Array.Empty<object?>()));
        }

        private sealed class Scope : IDisposable
        {
            private readonly Stack<object?> _stack;

            public Scope(Stack<object?> stack) => _stack = stack;

            public void Dispose() => _stack.TryPop(out _);
        }
    }

    private sealed class ThrowingProductsHandler : IGetProductsHandler
    {
        public Task<IReadOnlyList<ProductView>> HandleAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("boom-secreto");
    }
}
