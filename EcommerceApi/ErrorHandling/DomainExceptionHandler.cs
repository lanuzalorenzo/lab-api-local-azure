using EcommerceApi.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace EcommerceApi.ErrorHandling;

/// <summary>
/// Maps domain invariant violations to a 400 response. Expected input failures are
/// already rejected by model validation, so reaching here means the domain caught
/// something the transport layer could not (for example, an inconsistent price).
/// </summary>
public sealed class DomainExceptionHandler : IExceptionHandler
{
    private readonly ILogger<DomainExceptionHandler> _logger;

    public DomainExceptionHandler(ILogger<DomainExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DomainException domainException)
        {
            return false;
        }

        _logger.LogWarning(domainException, "A domain invariant was violated.");

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsJsonAsync(
            new { message = domainException.Message },
            cancellationToken);

        return true;
    }
}
