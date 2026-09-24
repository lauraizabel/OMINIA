namespace Ambev.DeveloperEvaluation.WebApi.Common;

public static class ApiErrorTypes
{
    public const string Authentication = "AuthenticationError";
    public const string Authorization = "AuthorizationError";
    public const string ConcurrencyConflict = "ConcurrencyConflict";
    public const string Conflict = "Conflict";
    public const string DependencyUnavailable = "DependencyUnavailable";
    public const string InvalidRequest = "InvalidRequest";
    public const string PayloadTooLarge = "PayloadTooLarge";
    public const string PreconditionRequired = "PreconditionRequired";
    public const string RateLimitExceeded = "RateLimitExceeded";
    public const string Request = "RequestError";
    public const string ResourceNotFound = "ResourceNotFound";
    public const string Server = "ServerError";
    public const string UnsupportedMediaType = "UnsupportedMediaType";
    public const string Validation = "ValidationError";
}
