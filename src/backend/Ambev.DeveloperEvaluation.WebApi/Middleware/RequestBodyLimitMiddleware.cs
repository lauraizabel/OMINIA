using Ambev.DeveloperEvaluation.WebApi.Common;
using Microsoft.AspNetCore.Http.Features;

namespace Ambev.DeveloperEvaluation.WebApi.Middleware;

public sealed class RequestBodyLimitMiddleware
{
    public const long MaximumBodySize = 256 * 1024;
    private readonly RequestDelegate _next;

    public RequestBodyLimitMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var bodySizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodySizeFeature is { IsReadOnly: false })
        {
            bodySizeFeature.MaxRequestBodySize = MaximumBodySize;
        }

        if (context.Request.ContentLength > MaximumBodySize)
        {
            await ApiErrorWriter.WriteAsync(
                context,
                StatusCodes.Status413PayloadTooLarge,
                ApiErrorTypes.PayloadTooLarge,
                "Request body is too large",
                "The request body cannot exceed 256 KiB.");
            return;
        }

        await _next(context);
    }
}
