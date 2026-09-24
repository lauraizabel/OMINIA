using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.WebApi.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ambev.DeveloperEvaluation.WebApi.Middleware;

public sealed class ApiExceptionMiddleware
{
    private const int ClientClosedRequestStatusCode = 499;
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiExceptionMiddleware> _logger;

    public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = ClientClosedRequestStatusCode;
            }
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            await HandleAsync(context, exception);
        }
    }

    private Task HandleAsync(HttpContext context, Exception exception)
    {
        return exception switch
        {
            ValidationException validation => WriteValidationAsync(context, validation),
            DomainValidationException validation => WriteDomainAsync(
                context, StatusCodes.Status400BadRequest, ApiErrorTypes.Validation, "Invalid input data", validation),
            DomainNotFoundException notFound => WriteDomainAsync(
                context, StatusCodes.Status404NotFound, ApiErrorTypes.ResourceNotFound, "Resource not found", notFound),
            DomainConflictException conflict => WriteDomainAsync(
                context, StatusCodes.Status409Conflict, ApiErrorTypes.Conflict, "The request conflicts with the current state", conflict),
            UnauthorizedAccessException => ApiErrorWriter.WriteAsync(
                context,
                StatusCodes.Status401Unauthorized,
                ApiErrorTypes.Authentication,
                "Authentication failed",
                "The supplied credentials are invalid."),
            DbUpdateConcurrencyException => ApiErrorWriter.WriteAsync(
                context,
                StatusCodes.Status412PreconditionFailed,
                ApiErrorTypes.ConcurrencyConflict,
                "The resource has changed",
                "Refresh the resource and retry with its current ETag."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                ApiErrorWriter.WriteAsync(
                    context,
                    StatusCodes.Status409Conflict,
                    ApiErrorTypes.Conflict,
                    "A unique value is already in use",
                    "The request conflicts with an existing resource."),
            BadHttpRequestException badRequest => ApiErrorWriter.WriteAsync(
                context,
                badRequest.StatusCode,
                badRequest.StatusCode == StatusCodes.Status413PayloadTooLarge ? ApiErrorTypes.PayloadTooLarge : ApiErrorTypes.InvalidRequest,
                badRequest.StatusCode == StatusCodes.Status413PayloadTooLarge ? "Request body is too large" : "Invalid request",
                badRequest.StatusCode == StatusCodes.Status413PayloadTooLarge
                    ? "The request body cannot exceed 256 KiB."
                    : "The server could not process the request."),
            NpgsqlException databaseException => WriteDependencyUnavailableAsync(context, databaseException),
            TimeoutException timeout => WriteDependencyUnavailableAsync(context, timeout),
            _ => WriteUnexpectedAsync(context, exception)
        };
    }

    private static Task WriteValidationAsync(HttpContext context, ValidationException exception)
    {
        var errors = exception.Errors
            .Select(failure => new ApiErrorDetail(
                ToCamelCase(failure.PropertyName),
                failure.ErrorCode,
                failure.ErrorMessage))
            .ToArray();

        return ApiErrorWriter.WriteAsync(
            context,
            StatusCodes.Status400BadRequest,
            ApiErrorTypes.Validation,
            "Invalid input data",
            "Correct the fields listed in errors.",
            errors);
    }

    private static Task WriteDomainAsync(
        HttpContext context,
        int statusCode,
        string type,
        string error,
        global::DomainException exception)
    {
        return ApiErrorWriter.WriteAsync(
            context,
            statusCode,
            type,
            error,
            exception.Message,
            [new ApiErrorDetail(string.Empty, exception.Code, exception.Message)]);
    }

    private Task WriteUnexpectedAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "Unhandled API exception. TraceId: {TraceId}", context.TraceIdentifier);

        return ApiErrorWriter.WriteAsync(
            context,
            StatusCodes.Status500InternalServerError,
            ApiErrorTypes.Server,
            "An unexpected error occurred",
            "The server could not complete the request.");
    }

    private Task WriteDependencyUnavailableAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "Required dependency unavailable. TraceId: {TraceId}", context.TraceIdentifier);

        return ApiErrorWriter.WriteAsync(
            context,
            StatusCodes.Status503ServiceUnavailable,
            ApiErrorTypes.DependencyUnavailable,
            "A required dependency is unavailable",
            "Try the request again later.");
    }

    private static string ToCamelCase(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];
}
