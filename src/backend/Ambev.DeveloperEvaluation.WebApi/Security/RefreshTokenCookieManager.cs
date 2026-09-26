using Ambev.DeveloperEvaluation.Application.Auth.RefreshSessions;
using Microsoft.Extensions.Options;

namespace Ambev.DeveloperEvaluation.WebApi.Security;

public sealed class RefreshTokenCookieManager
{
    private readonly RefreshSessionOptions _options;
    private readonly HashSet<string> _allowedOrigins;

    public RefreshTokenCookieManager(
        IOptions<RefreshSessionOptions> options,
        IConfiguration configuration)
    {
        _options = options.Value;
        _allowedOrigins = (configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
            .Select(NormalizeOrigin)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public string? Read(HttpRequest request) => request.Cookies[_options.CookieName];

    public void Write(HttpResponse response, string token, DateTimeOffset expiresAt)
    {
        response.Cookies.Append(_options.CookieName, token, CookieOptions(expiresAt));
    }

    public void Delete(HttpResponse response)
    {
        response.Cookies.Delete(_options.CookieName, CookieOptions(null));
    }

    public bool IsTrustedOrigin(HttpRequest request)
    {
        var origin = request.Headers.Origin.ToString();
        if (string.IsNullOrWhiteSpace(origin))
            return true;

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
            return false;

        var requestOrigin = NormalizeOrigin($"{request.Scheme}://{request.Host}");
        return string.Equals(NormalizeOrigin(originUri.GetLeftPart(UriPartial.Authority)), requestOrigin, StringComparison.OrdinalIgnoreCase)
            || _allowedOrigins.Contains(NormalizeOrigin(originUri.GetLeftPart(UriPartial.Authority)));
    }

    private CookieOptions CookieOptions(DateTimeOffset? expiresAt) => new()
    {
        HttpOnly = true,
        Secure = _options.SecureCookie,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        IsEssential = true,
        Expires = expiresAt
    };

    private static string NormalizeOrigin(string origin) => origin.TrimEnd('/');
}
