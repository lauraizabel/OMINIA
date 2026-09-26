using System.Text;
using Ambev.DeveloperEvaluation.WebApi.Middleware;
using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.WebApi.Features.Sales;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
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

    public static TheoryData<Exception, int, string> MappedExceptions => new()
    {
        { new DomainValidationException("Sale.Invalid", "invalid sale"), 400, "ValidationError" },
        { new DomainNotFoundException("Sale.NotFound", "missing sale"), 404, "ResourceNotFound" },
        { new DomainConflictException("Sale.Conflict", "conflicting sale"), 409, "Conflict" },
        { new DomainConcurrencyException("Sale.Version", "stale sale"), 412, "ConcurrencyConflict" },
        { HttpPreconditionException.Required(), 428, "PreconditionRequired" },
        { HttpPreconditionException.Malformed(), 400, "InvalidRequest" },
        { new UnauthorizedAccessException("sensitive authentication reason"), 401, "AuthenticationError" },
        { new DbUpdateConcurrencyException("database detail"), 412, "ConcurrencyConflict" },
        { new BadHttpRequestException("large", 413), 413, "PayloadTooLarge" },
        { new BadHttpRequestException("bad", 400), 400, "InvalidRequest" },
        { new TimeoutException("database timeout detail"), 503, "DependencyUnavailable" }
    };

    [Theory]
    [MemberData(nameof(MappedExceptions))]
    public async Task Known_exception_is_mapped_to_public_problem(
        Exception exception,
        int expectedStatus,
        string expectedType)
    {
        var (context, body) = await InvokeAsync(exception);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Contains(expectedType, body, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive authentication reason", body, StringComparison.Ordinal);
        Assert.DoesNotContain("database timeout detail", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fluent_validation_errors_are_camel_cased_and_returned_as_details()
    {
        var exception = new ValidationException([
            new ValidationFailure("SaleNumber", "Sale number is required") { ErrorCode = "NotEmpty" },
            new ValidationFailure(string.Empty, "Request is invalid") { ErrorCode = "Invalid" }
        ]);

        var (context, body) = await InvokeAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Contains("saleNumber", body, StringComparison.Ordinal);
        Assert.Contains("NotEmpty", body, StringComparison.Ordinal);
        Assert.Contains("Request is invalid", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Aborted_request_returns_499_without_a_response_body()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var middleware = new ApiExceptionMiddleware(
            _ => throw new OperationCanceledException(source.Token),
            NullLogger<ApiExceptionMiddleware>.Instance);
        var context = new DefaultHttpContext { RequestAborted = source.Token };
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(499, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    [Fact]
    public async Task Exception_after_response_started_is_not_rewritten()
    {
        var middleware = new ApiExceptionMiddleware(
            _ => throw new InvalidOperationException("late failure"),
            NullLogger<ApiExceptionMiddleware>.Instance);
        var context = new DefaultHttpContext();
        var responseFeature = Substitute.For<IHttpResponseFeature>();
        responseFeature.HasStarted.Returns(true);
        context.Features.Set(responseFeature);

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
    }

    private static async Task<(DefaultHttpContext Context, string Body)> InvokeAsync(Exception exception)
    {
        var middleware = new ApiExceptionMiddleware(
            _ => throw exception,
            NullLogger<ApiExceptionMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        return (context, await reader.ReadToEndAsync());
    }
}
