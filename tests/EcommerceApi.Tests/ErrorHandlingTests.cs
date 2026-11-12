using EcommerceApi.ErrorHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EcommerceApi.Tests;

public sealed class ErrorHandlingTests
{
    [Fact]
    public async Task BadHttpRequestExceptionHandler_MapsPayloadTooLargeWithoutLeakingTheMessage()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        using var provider = services.BuildServiceProvider();
        var problemDetails = provider.GetRequiredService<IProblemDetailsService>();
        var handler = new BadHttpRequestExceptionHandler(
            problemDetails,
            NullLogger<BadHttpRequestExceptionHandler>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var exception = new Microsoft.AspNetCore.Http.BadHttpRequestException(
            "internal Kestrel detail",
            StatusCodes.Status413PayloadTooLarge);

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("\"status\":413", body);
        Assert.DoesNotContain("internal Kestrel detail", body);
    }

    [Fact]
    public async Task BadHttpRequestExceptionHandler_IgnoresOtherExceptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        using var provider = services.BuildServiceProvider();
        var handler = new BadHttpRequestExceptionHandler(
            provider.GetRequiredService<IProblemDetailsService>(),
            NullLogger<BadHttpRequestExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(
            new DefaultHttpContext(),
            new InvalidOperationException("not a bad request"),
            CancellationToken.None);

        Assert.False(handled);
    }
}
