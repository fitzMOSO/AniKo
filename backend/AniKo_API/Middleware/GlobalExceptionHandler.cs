using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AniKo_API.Middleware;

/// <summary>
/// Converts any unhandled exception into an RFC 7807 ProblemDetails response.
/// The exception detail goes to the log, never to the client.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Not every exception reaching here is a server fault. Model binding throws
        // BadHttpRequestException — carrying its own 400 — when a required query parameter is
        // missing, and that is the caller's mistake, not ours.
        //
        // This handler used to answer 500 unconditionally, which was invisible until an endpoint
        // first had a parameter that could be missing. It caused two separate problems:
        // a caller omitting `lat` was told the server was broken and to retry, when retrying
        // could never help; and every such request was logged at Error, so anyone scanning the
        // query string would bury real faults under their own typos.
        var isClientError = exception is BadHttpRequestException;
        var statusCode = exception is BadHttpRequestException badRequest
            ? badRequest.StatusCode
            : StatusCodes.Status500InternalServerError;

        if (isClientError)
        {
            // Warning, not Error: a malformed request is expected traffic on a public API.
            // The message is included because it names the offending parameter and is written
            // by the framework, not by the caller — there is no user input echoed here.
            _logger.LogWarning(
                "Rejected {Method} {Path}: {Reason}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                exception.Message);
        }
        else
        {
            _logger.LogError(
                exception,
                "Unhandled exception on {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            // The 400 title names the parameter; the 500 title deliberately says nothing, because
            // an internal fault's detail belongs in the log and not in the response.
            Title = isClientError
                ? "The request could not be bound."
                : "An error occurred while processing your request",
            Detail = isClientError ? exception.Message : null,
            Type = isClientError
                ? "https://tools.ietf.org/html/rfc9110#section-15.5.1"
                : "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            Instance = httpContext.Request.Path,
        };

        httpContext.Response.StatusCode = statusCode;

        // contentType is passed explicitly because WriteAsJsonAsync otherwise stamps
        // "application/json". That was wrong in a way nothing would surface: the body is already
        // a ProblemDetails and RFC 9457 gives it its own media type, so this handler and
        // TypedResults.ValidationProblem — which does set "application/problem+json" — were
        // emitting the same shape under two different labels. A client that branches on the
        // media type to decide whether a body is a problem document would parse the validator's
        // 400 and fall through on this one, for identical-looking JSON.
        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: "application/problem+json",
            cancellationToken);

        return true;
    }
}
