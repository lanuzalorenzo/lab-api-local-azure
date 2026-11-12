using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceApi.ErrorHandling;

/// <summary>
/// Preserves the status code Kestrel assigns when it rejects a request before MVC
/// reaches the action. A body over the declared limit must surface as 413, not as
/// the generic 500 that the default exception handler would produce.
/// </summary>
public sealed class BadHttpRequestExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<BadHttpRequestExceptionHandler> _logger;

    public BadHttpRequestExceptionHandler(
        IProblemDetailsService problemDetails,
        ILogger<BadHttpRequestExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not Microsoft.AspNetCore.Http.BadHttpRequestException badRequest)
        {
            return false;
        }

        var statusCode = badRequest.StatusCode is >= 400 and <= 599
            ? badRequest.StatusCode
            : StatusCodes.Status400BadRequest;

        _logger.LogWarning(
            badRequest,
            "Kestrel rejected the request with status {StatusCode}.",
            statusCode);

        httpContext.Response.StatusCode = statusCode;

        await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = badRequest,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = statusCode == StatusCodes.Status413PayloadTooLarge
                    ? "Payload too large"
                    : "Invalid request",
                Detail = statusCode == StatusCodes.Status413PayloadTooLarge
                    ? "The request body exceeds the allowed size."
                    : "The request could not be processed."
            }
        });

        return true;
    }
}
