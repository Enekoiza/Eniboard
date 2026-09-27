using Application.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace Api.Middleware;

/// <summary>
/// Global exception handler mapping application exceptions to RFC 7807 Problem Details
/// responses: FluentValidation/validation failures -> 400, not-found -> 404,
/// WIP-limit violations -> 409, malformed requests (BadHttpRequestException) -> their own
/// status code (usually 400), anything else -> 500. 500 responses carry a generic
/// detail message; the underlying exception is logged, not exposed to the client.
/// </summary>
public sealed class ProblemDetailsExceptionHandler(ILogger<ProblemDetailsExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            ValidationAppException => (StatusCodes.Status400BadRequest, "Validation failed"),
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            WipLimitExceededException => (StatusCodes.Status409Conflict, "WIP limit exceeded"),
            BadHttpRequestException badRequest => (badRequest.StatusCode, "Bad request"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred"),
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception processing {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = statusCode;

        var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = statusCode == StatusCodes.Status500InternalServerError ? "An unexpected error occurred. Please try again later." : exception.Message,
            Instance = httpContext.Request.Path,
        };

        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions["errors"] = validationException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        }

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
