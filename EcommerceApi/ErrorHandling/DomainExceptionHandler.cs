using EcommerceApi.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceApi.ErrorHandling;

/// <summary>
/// Maps domain invariant violations to a 400 ProblemDetails response. Expected input
/// failures are already rejected by model validation, so reaching here means the
/// domain caught something the transport layer could not (for example, an
/// inconsistent price). The handler is a safety net: with the current endpoints the
/// transport contract covers the same rules first.
/// </summary>
public sealed class DomainExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<DomainExceptionHandler> _logger;

    public DomainExceptionHandler(
        IProblemDetailsService problemDetails,
        ILogger<DomainExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

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

        await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = domainException,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid request",
                Detail = domainException.Message
            }
        });

        return true;
    }
}
