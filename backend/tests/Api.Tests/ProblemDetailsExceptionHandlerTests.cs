using System.Text.Json;
using Api.Middleware;
using Application.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests;

public class ProblemDetailsExceptionHandlerTests
{
    private static async Task<JsonDocument> InvokeAsync(Exception exception, HttpContext httpContext)
    {
        var handler = new ProblemDetailsExceptionHandler(NullLogger<ProblemDetailsExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);
        Assert.True(handled);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonDocument.ParseAsync(httpContext.Response.Body);
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        return new DefaultHttpContext
        {
            Response = { Body = new MemoryStream() },
            Request = { Path = "/test" },
        };
    }

    [Fact]
    public async Task TryHandleAsync_WithUnhandledException_ReturnsGenericDetailAndHidesMessage()
    {
        var httpContext = CreateHttpContext();
        var exception = new InvalidOperationException("secret db connection string");

        using var document = await InvokeAsync(exception, httpContext);

        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
        Assert.Equal("An unexpected error occurred. Please try again later.", document.RootElement.GetProperty("detail").GetString());

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        var body = await reader.ReadToEndAsync();
        Assert.DoesNotContain("secret db connection string", body);
    }

    [Fact]
    public async Task TryHandleAsync_WithWipLimitExceededException_ReturnsConflictWithExceptionMessage()
    {
        var httpContext = CreateHttpContext();
        var exception = new WipLimitExceededException("WIP limit of 3 exceeded for column 'In Progress'");

        using var document = await InvokeAsync(exception, httpContext);

        Assert.Equal(StatusCodes.Status409Conflict, httpContext.Response.StatusCode);
        Assert.Equal(exception.Message, document.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task TryHandleAsync_WithNotFoundException_ReturnsNotFoundWithExceptionMessage()
    {
        var httpContext = CreateHttpContext();
        var exception = new NotFoundException("Card not found");

        using var document = await InvokeAsync(exception, httpContext);

        Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
        Assert.Equal(exception.Message, document.RootElement.GetProperty("detail").GetString());
    }
}
