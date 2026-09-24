using System.Threading.RateLimiting;
using System.Text.Json.Serialization;
using Ambev.DeveloperEvaluation.Application;
using Ambev.DeveloperEvaluation.Domain.Enums;
using Ambev.DeveloperEvaluation.WebApi.Common;
using Ambev.DeveloperEvaluation.WebApi.Middleware;
using Ambev.DeveloperEvaluation.WebApi.Security;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Server.IIS;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

namespace Ambev.DeveloperEvaluation.WebApi.Configuration;

public static class ApiConfigurationExtensions
{
    private const string CorsPolicy = "ConfiguredOrigins";

    public static IServiceCollection AddApiProtection(this IServiceCollection services)
    {
        services.Configure<KestrelServerOptions>(options =>
            options.Limits.MaxRequestBodySize = RequestBodyLimitMiddleware.MaximumBodySize);
        services.Configure<IISServerOptions>(options =>
            options.MaxRequestBodySize = RequestBodyLimitMiddleware.MaximumBodySize);

        services.AddControllers()
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.SuppressMapClientErrors = true;
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .SelectMany(entry => entry.Value?.Errors.Select(error => new ApiErrorDetail(
                        ToCamelCase(entry.Key),
                        "InvalidValue",
                        string.IsNullOrWhiteSpace(error.ErrorMessage) ? "The supplied value is invalid." : error.ErrorMessage))
                        ?? [])
                    .ToArray();

                var response = new ApiErrorResponse(
                    ApiErrorTypes.Validation,
                    "Invalid input data",
                    "Correct the fields listed in errors.",
                    context.HttpContext.TraceIdentifier,
                    errors);

                return new BadRequestObjectResult(response)
                {
                    ContentTypes = { "application/problem+json" }
                };
            };
        });

        services.AddValidatorsFromAssembly(typeof(ApplicationLayer).Assembly);

        services.AddAuthorizationBuilder()
            .AddPolicy(ApiPolicies.Administrators, policy =>
                policy.RequireAuthenticatedUser().RequireRole(nameof(UserRole.Admin)))
            .AddPolicy(ApiPolicies.SalesOperators, policy =>
                policy.RequireAuthenticatedUser().RequireRole(nameof(UserRole.Manager), nameof(UserRole.Admin)));

        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<IConfiguration>((options, currentConfiguration) =>
        {
            var allowedOrigins = currentConfiguration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            options.AddPolicy(CorsPolicy, policy =>
            {
                if (allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                }
            });
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, _) =>
            {
                if (!context.HttpContext.Response.HasStarted)
                {
                    await ApiErrorWriter.WriteAsync(
                        context.HttpContext,
                        StatusCodes.Status429TooManyRequests,
                        ApiErrorTypes.RateLimitExceeded,
                        "Too many requests",
                        "Wait before attempting to authenticate again.");
                }
            };

            options.AddPolicy(RateLimitPolicies.Login, context =>
            {
                var settings = context.RequestServices.GetRequiredService<IOptions<LoginRateLimitOptions>>().Value;
                return RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.PermitLimit,
                        Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });
        });

        services.AddOptions<LoginRateLimitOptions>()
            .BindConfiguration(LoginRateLimitOptions.SectionName)
            .Validate(options => options.PermitLimit is > 0 and <= 100, "Login rate limit must be between 1 and 100 requests.")
            .Validate(options => options.WindowSeconds is >= 1 and <= 3600, "Login rate-limit window must be between 1 and 3600 seconds.")
            .ValidateOnStart();

        return services;
    }

    public static WebApplication UseApiProtection(this WebApplication app)
    {
        app.UseMiddleware<ApiExceptionMiddleware>();
        app.UseMiddleware<RequestBodyLimitMiddleware>();
        app.UseStatusCodePages(async statusCodeContext =>
        {
            var context = statusCodeContext.HttpContext;
            var (type, error, detail) = context.Response.StatusCode switch
            {
                StatusCodes.Status401Unauthorized => (ApiErrorTypes.Authentication, "Authentication is required", "Supply a valid bearer token."),
                StatusCodes.Status403Forbidden => (ApiErrorTypes.Authorization, "Access is forbidden", "The authenticated identity cannot perform this operation."),
                StatusCodes.Status404NotFound => (ApiErrorTypes.ResourceNotFound, "Resource not found", "The requested resource does not exist."),
                StatusCodes.Status415UnsupportedMediaType => (ApiErrorTypes.UnsupportedMediaType, "Unsupported media type", "Send the request using a supported content type."),
                _ => (ApiErrorTypes.Request, "The request could not be completed", "Review the request and try again.")
            };

            await ApiErrorWriter.WriteAsync(context, context.Response.StatusCode, type, error, detail);
        });

        app.UseCors(CorsPolicy);
        app.UseRateLimiter();
        return app;
    }

    private static string ToCamelCase(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];
}
