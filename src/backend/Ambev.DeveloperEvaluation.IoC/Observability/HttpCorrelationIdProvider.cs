using Ambev.DeveloperEvaluation.Application.Observability;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;

namespace Ambev.DeveloperEvaluation.IoC.Observability;

public sealed class HttpCorrelationIdProvider : ICorrelationIdProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private string? _fallbackCorrelationId;

    public HttpCorrelationIdProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string CorrelationId =>
        _httpContextAccessor.HttpContext?.TraceIdentifier
        ?? Activity.Current?.TraceId.ToString()
        ?? (_fallbackCorrelationId ??= Guid.NewGuid().ToString("N"));
}
