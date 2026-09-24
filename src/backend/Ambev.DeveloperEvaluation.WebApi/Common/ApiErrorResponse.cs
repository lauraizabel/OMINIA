using System.Text.Json.Serialization;

namespace Ambev.DeveloperEvaluation.WebApi.Common;

public sealed record ApiErrorResponse(
    string Type,
    string Error,
    string Detail,
    string TraceId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyCollection<ApiErrorDetail>? Errors = null);

public sealed record ApiErrorDetail(string Field, string Code, string Message);
