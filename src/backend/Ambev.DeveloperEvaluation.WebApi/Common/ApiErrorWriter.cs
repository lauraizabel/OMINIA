using System.Text.Json;

namespace Ambev.DeveloperEvaluation.WebApi.Common;

public static class ApiErrorWriter
{
    public static async Task WriteAsync(
        HttpContext context,
        int statusCode,
        string type,
        string error,
        string detail,
        IReadOnlyCollection<ApiErrorDetail>? errors = null)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var response = new ApiErrorResponse(
            type,
            error,
            detail,
            context.TraceIdentifier,
            errors is { Count: > 0 } ? errors : null);

        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            response,
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            context.RequestAborted);
    }
}
