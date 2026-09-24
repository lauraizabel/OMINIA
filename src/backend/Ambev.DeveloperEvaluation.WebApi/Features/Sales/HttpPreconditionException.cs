namespace Ambev.DeveloperEvaluation.WebApi.Features.Sales;

public sealed class HttpPreconditionException : Exception
{
    public int StatusCode { get; }

    private HttpPreconditionException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    public static HttpPreconditionException Required() => new(
        StatusCodes.Status428PreconditionRequired,
        "The If-Match header is required.");

    public static HttpPreconditionException Malformed() => new(
        StatusCodes.Status400BadRequest,
        "If-Match must contain exactly one strong sale ETag.");
}
