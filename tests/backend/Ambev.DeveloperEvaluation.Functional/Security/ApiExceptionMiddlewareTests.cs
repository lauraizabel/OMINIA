using System.Text;
using Ambev.DeveloperEvaluation.WebApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ambev.DeveloperEvaluation.Functional.Security;

public sealed class ApiExceptionMiddlewareTests
{
    [Fact]
    public async Task Unexpected_exception_returns_generic_500_without_internal_details()
    {
        const string sensitiveDetail = "database-password-and-stack-trace";
        var middleware = new ApiExceptionMiddleware(
            _ => throw new InvalidOperationException(sensitiveDetail),
            NullLogger<ApiExceptionMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Contains("ServerError", body, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveDetail, body, StringComparison.Ordinal);
    }
}
